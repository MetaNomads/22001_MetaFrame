using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Piglet
{
	/// <summary>
	/// Sequentially executes a set of subtasks (coroutines) in order
	/// to import a glTF model. Each subtask corresponds to importing a
	/// different type of glTF entity (buffers, textures, materials,
	/// meshes, etc.).
	///
	/// In principle, this class could be replaced by a simple wrapper
	/// coroutine method that iterates through the subtask coroutines in
	/// sequence.  However, this class provides the additional abilities
	/// to: (1) abort the import process, (2) specify user-defined
	/// callbacks for abortion/exception/completion, and (3) check the
	/// current execution state of the import task
	/// (running/aborted/exception/completed).
	/// </summary>
	public class GltfImportTask : IEnumerator
	{
		/// <summary>
		/// The possible execution states of an import task.
		/// </summary>
		public enum ExecutionState
		{
			Running,
			Aborted,
			Exception,
			Completed
		};

		/// <summary>
		/// The current execution state of this import task (e.g. aborted).
		/// </summary>
		public ExecutionState State;

		/// <summary>
		/// Callback(s) that are invoked to report
		/// intermediate progress during a glTF import.
		/// </summary>
		public GltfImporter.ProgressCallback OnProgress;

		/// <summary>
		/// Prototype for callbacks that are invoked when
		/// a glTF import is aborted by the user.
		/// </summary>
		public delegate void AbortedCallback();

		/// <summary>
		/// Callback(s) that are invoked when the glTF import is
		/// aborted by the user. This provides a
		/// useful hook for cleaning up the aborted import task.
		/// </summary>
		public AbortedCallback OnAborted;

		/// <summary>
		/// Prototype for callbacks that are invoked when
		/// an exception occurs during a glTF import.
		/// </summary>
		public delegate void ExceptionCallback(Exception e);

		/// <summary>
		/// Callback(s) that are invoked when an exception
		/// is thrown during a glTF import. This provides a
		/// useful hook for cleaning up a failed import task
		/// and/or presenting error messages to the user.
		/// </summary>
		public ExceptionCallback OnException;

		/// <summary>
		/// If true, an exception will be rethrown after
		/// being passed to user-defined exception callbacks
		/// in OnException.
		/// </summary>
		public bool RethrowExceptionAfterCallbacks;

		/// <summary>
		/// Prototype for callbacks that are invoked when
		/// the glTF import task has successfully completed.
		/// </summary>
		public delegate void CompletedCallback(GameObject importedModel);

		/// <summary>
		/// Callback(s) that are invoked when the glTF import
		/// successfully completes.  The root GameObject of
		/// the imported model is passed as argument to these
		/// callbacks.
		/// </summary>
		public CompletedCallback OnCompleted;

		/// <summary>
		/// <para>
		/// The list of subtasks (coroutines) that make up
		/// the overall glTF import task.
		/// </para>
		/// <para>
		/// The first item in the tuple is the user-defined name
		/// for the task, which is used when printing profiling
		/// data to the Unity log file. The second item in
		/// the tuple is the IEnumerator used for executing
		/// the task.
		/// </para>
		/// </summary>
		List<(string, IEnumerator)> _tasks;

		/// <summary>
		/// Maximum number of milliseconds that MoveNext() should execute
		/// before returning control back to the main Unity thread.
		/// </summary>
		public int MillisecondsPerYield;

		/// <summary>
		/// Set this to true to record profiling data using SimpleProfiler.
		/// </summary>
		public bool ProfilingEnabled;

		/// <summary>
		/// Stopwatch used to track time spent in MoveNext(). We
		/// do as much work as possible per frame, but stop as
		/// soon as we exceed MillisecondsPerYield. This prevents
		/// unnecessarily stalling of the main Unity thread
		/// during glTF imports (i.e. frame rate drops).
		/// </summary>
		private Stopwatch _moveNextStopwatch;

		/// <summary>
		/// Measures the wallclock time of individual tasks.
		/// </summary>
		private Stopwatch _taskStopwatch;

		/// <summary>
		/// The longest invocation of this class's MoveNext() method
		/// across the entire glTF import.
		/// </summary>
		public long LongestMoveNextInMilliseconds
		{
			get
			{
				if (SimpleProfiler.Instance.Results.TryGetValue(
					"GltfImportTask.MoveNext", out var hist))
				{
					if (hist.Max.HasValue)
						return hist.Max.Value;
				}

				return 0;
			}
		}

		public GltfImportTask()
		{
			_tasks = new List<(string, IEnumerator)>();

			_moveNextStopwatch = new Stopwatch();
			_taskStopwatch = new Stopwatch();

			State = ExecutionState.Running;
			RethrowExceptionAfterCallbacks = true;

			// For runtime glTF imports, target 60 fps (16 ms per frame).
			// For Editor imports, run the whole task in one MoveNext call
			// to minimize the overall import time.
			MillisecondsPerYield = Application.isPlaying ? 16 : 100;

			// Profiling has some extra CPU and memory overhead. It is probably
			// not much, but for maximum performance it is best to disable it by
			// default.
			ProfilingEnabled = false;
		}

		/// <summary>
		/// Add a subtask to the front of the subtask list.
		/// </summary>
		/// <param name="name">
		/// The user-defined name for the task. This is
		/// used for identifying the task in the profiling data.
		/// </param>
		/// <param name="task">
		/// The IEnumerator that will be used to execute the task.
		/// </param>
		public void PushTask(string name, IEnumerator task)
		{
			_tasks.Insert(0, (name, task));
		}

		/// <summary>
		/// Add a subtask to the front of the subtask list.
		/// </summary>
		/// <param name="name">
		/// The user-defined name for the task. This is
		/// used for identifying the task in the profiling data.
		/// </param>
		/// <param name="action">
		/// The Action that will be used to execute the task.
		/// </param>
		public void PushTask(string name, Action action)
		{
			IEnumerator ActionWrapper()
			{
				action.Invoke();
				yield return null;
			}

			_tasks.Insert(0, (name, ActionWrapper()));
		}

		/// <summary>
		/// Add a subtask to be executed during the import process.
		/// Subtasks are typically used for importing different
		/// types of glTF entities (e.g. buffers, textures, meshes).
		/// Subtasks are executed in the order that they are added.
		/// </summary>
		/// <param name="name">
		/// The user-defined name for the task. This is
		/// used for identifying the task in the profiling data.
		/// </param>
		/// <param name="task">
		/// The IEnumerator that will be used to execute the task.
		/// </param>
		public void AddTask(string name, IEnumerator task)
		{
			_tasks.Add((name, task));
		}

		/// <summary>
		/// Add a subtask to be executed during the import process.
		/// Subtasks are typically used for importing different
		/// types of glTF entities (e.g. buffers, textures, meshes).
		/// Subtasks are executed in the order that they are added.
		/// </summary>
		/// <param name="name">
		/// The user-defined name for the task. This is
		/// used for identifying the task in the profiling data.
		/// </param>
		/// <param name="task">
		/// The IEnumerable that will be used to execute the task.
		/// </param>
		public void AddTask(string name, IEnumerable task)
		{
			_tasks.Add((name, task.GetEnumerator()));
		}

		/// <summary>
		/// Add a subtask for a C# Action (i.e. a method with zero arguments
		/// that does not return a value).
		/// </summary>
		/// <param name="name">
		/// The user-defined name for the task. This is
		/// used for identifying the task in the profiling data.
		/// </param>
		/// <param name="action">
		/// The Action that will be used to execute the task.
		/// </param>
		public void AddTask(string name, Action action)
		{
			IEnumerator ActionWrapper()
			{
				action.Invoke();
				yield return null;
			}

			AddTask(name, ActionWrapper());
		}

		/// <summary>
		/// Abort this import task.
		/// </summary>
		public void Abort()
		{
			State = ExecutionState.Aborted;
			OnAborted?.Invoke();
			Clear();
		}

		/// <summary>
		/// Clear the list of subtasks.
		/// </summary>
		public void Clear()
		{
			_tasks.Clear();
		}

		/// <summary>
		/// Advance execution of the current subtask by a single step.
		/// </summary>
		public bool MoveNext()
		{
			if (State != ExecutionState.Running)
				return false;

			// The `progress` flag is used to ensure that every invocation
			// of this method (`GltfImportTask.MoveNext`) does at least
			// one piece of real work, i.e. makes at least one successful
			// `MoveNext` call on a task. This is important because it
			// prevents the `MillisecondsPerYield` time limit from causing
			// an infinite loop when value is set too low (e.g. zero).

			var progress = false;

			try
			{
				// Tracks how long we spend in this method (GltfImportTask.MoveNext).
				// We want to do as much work as possible per frame but we need to
				// stop as soon as we exceed the MillisecondsPerYield limit (to
				// avoid stalling the main Unity thread).

				_moveNextStopwatch.Restart();

				// Start wallclock time measurement for the first task, if needed.

				if (ProfilingEnabled && !_taskStopwatch.IsRunning)
					_taskStopwatch.Restart();

				while (_tasks.Count > 0 &&
				       (_moveNextStopwatch.ElapsedMilliseconds < MillisecondsPerYield || !progress))
				{
					if (ProfilingEnabled)
						SimpleProfiler.Instance.BeginSample($"{_tasks[0].Item1}.MoveNext");

					var moveNext = _tasks[0].Item2.MoveNext();

					if (ProfilingEnabled)
						SimpleProfiler.Instance.EndSample();

					progress |= moveNext;

					if (moveNext
					    && Current is YieldType yieldType
					    && yieldType == YieldType.Blocked)
					{
						break;
					}

					if (!moveNext)
					{
						if (ProfilingEnabled)
						{
							// record wallclock time for the completed task
							_taskStopwatch.Stop();

							SimpleProfiler.Instance.AddSample($"{_tasks[0].Item1}.Wallclock",
								(int)_taskStopwatch.ElapsedMilliseconds);
						}

						// if we just completed the last task
						if (_tasks.Count == 1)
						{
							State = ExecutionState.Completed;
							OnCompleted?.Invoke((GameObject) Current);
						}

						// remove completed task
						_tasks.RemoveAt(0);

						// start recording wallclock time for the next task (if any)
						if (ProfilingEnabled && _tasks.Count > 0)
							_taskStopwatch.Restart();
					}
				}
			}
			catch (Exception e)
			{
				State = ExecutionState.Exception;
				OnException?.Invoke(e);
				Clear();

				if (RethrowExceptionAfterCallbacks)
					throw;

				return false;
			}
			finally
			{
				_moveNextStopwatch.Stop();

				if (ProfilingEnabled)
				{
					SimpleProfiler.Instance.AddSample("GltfImportTask.MoveNext",
						(int) _moveNextStopwatch.ElapsedMilliseconds);
				}
			}

			return progress;
		}

		/// <summary>
		/// <para>
		/// This method is a stub and always throws
		/// NotImplementedException().
		/// </para>
		/// <para>
		/// The `Reset()` method is required by the IEnumerator
		/// interface, but does not serve any useful purpose
		/// for this particular class (`GltfImportTask`).
		/// </para>
		/// </summary>
		public void Reset()
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// <para>
		/// The last value returned by `yield return` for the
		/// currently executing subtask (coroutine).
		/// </para>
		/// <para>
		/// The value of `Current` is generally not of
		/// interest to Piglet users until the entire glTF import
		/// has completed successfully, in which case `Current`
		/// is a reference to the root `GameObject` of the imported
		/// model.
		/// </para>
		/// </summary>
		public object Current
		{
			get
			{
				if (_tasks.Count == 0)
					return null;

				return _tasks[0].Item2.Current;
			}
		}
	}
}