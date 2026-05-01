// ── Contract.cs ───────────────────────────────────────────────────────────────
// A tiny contract API for asserting preconditions, postconditions, invariants,
// and recoverable conditions inside MetaFrame components.
//
// Design goals:
//   - Negligible runtime cost in release builds (the hot-path methods are
//     Conditional("UNITY_EDITOR") + Conditional("METAFRAME_CONTRACTS") so
//     the call sites disappear from non-editor builds unless you explicitly
//     opt in by defining METAFRAME_CONTRACTS in Player Settings).
//   - Loud, contextual failures during development. Every check carries a
//     human-readable reason and a context object for the Console double-click.
//   - Optional self-repair via Healed(): check a condition, run a repair
//     action if it fails, and continue. Always log the repair so it can never
//     silently mask a deeper bug.
//
// Usage:
//   Contract.Require(grabbable != null, "grabbable not assigned", this);
//   Contract.Ensure(_currentIndex >= 0, "currentIndex went negative", this);
//   Contract.Invariant(_pendingActions == _activeActions.Count,
//                      "pendingActions drifted", this);
//   Contract.Healed(
//       () => _pendingActions == _activeActions.Count,
//       () => _pendingActions = _activeActions.Count,
//       "reconciled pendingActions to actual count",
//       this);

using System;
using System.Diagnostics;
using UnityEngine;
using Object = UnityEngine.Object;
// FIX: `using System.Diagnostics;` brings in `System.Diagnostics.Debug`, which
// would clash with `UnityEngine.Debug` whenever we call Debug.LogWarning /
// Debug.LogError below. Alias resolves the ambiguity in this file's favour
// without losing access to [Conditional].
using Debug = UnityEngine.Debug;

namespace MetaFrame.Contracts
{
    /// <summary>
    /// Severity level for contract violations. Determines how the framework
    /// surfaces the failure (warning vs error vs throw).
    /// </summary>
    public enum ContractSeverity
    {
        /// <summary>Log a warning. Continue execution. Best for self-heal paths.</summary>
        Warn,

        /// <summary>Log an error. Continue execution. Default for Require/Ensure/Invariant.</summary>
        Error,

        /// <summary>Throw a ContractViolationException. Use for unrecoverable preconditions.</summary>
        Throw,
    }

    [Serializable]
    public class ContractViolationException : Exception
    {
        public ContractViolationException(string message) : base(message) { }
    }

    /// <summary>
    /// Static contract API. All methods are conditional on UNITY_EDITOR or
    /// the METAFRAME_CONTRACTS preprocessor symbol — in a stripped player
    /// build, the call sites compile to nothing.
    ///
    /// Set <see cref="DefaultSeverity"/> at app start (or never — Error is
    /// the sensible default) to control how violations surface project-wide.
    /// </summary>
    public static class Contract
    {
        /// <summary>
        /// Default severity for Require / Ensure / Invariant when the caller
        /// doesn't specify one. Healed() always defaults to Warn.
        /// </summary>
        public static ContractSeverity DefaultSeverity = ContractSeverity.Error;

        /// <summary>
        /// True if any contract violation has been observed in this session.
        /// Useful for in-app diagnostic banners or test-harness sanity checks.
        /// </summary>
        public static bool AnyViolations { get; private set; }

        /// <summary>
        /// Total number of violations (including healed) seen in this session.
        /// Reset with ResetCounters().
        /// </summary>
        public static int TotalViolations { get; private set; }

        /// <summary>Total number of healed violations.</summary>
        public static int TotalHealed { get; private set; }

        /// <summary>Reset the session-wide counters. Used by the audit test harness.</summary>
        public static void ResetCounters()
        {
            AnyViolations   = false;
            TotalViolations = 0;
            TotalHealed     = 0;
        }

        // ── Require — precondition (caller's responsibility) ─────────────────

        /// <summary>
        /// Precondition. Use at the top of a method to assert what the caller
        /// must have already arranged (e.g. arguments not null, state set up).
        /// </summary>
        [Conditional("UNITY_EDITOR"), Conditional("METAFRAME_CONTRACTS")]
        public static void Require(bool condition, string reason, Object context = null)
        {
            if (!condition) Fail("Require", reason, context, DefaultSeverity);
        }

        /// <summary>Require with explicit severity.</summary>
        [Conditional("UNITY_EDITOR"), Conditional("METAFRAME_CONTRACTS")]
        public static void Require(bool condition, string reason, ContractSeverity severity, Object context = null)
        {
            if (!condition) Fail("Require", reason, context, severity);
        }

        // ── Ensure — postcondition (this method's responsibility) ────────────

        /// <summary>
        /// Postcondition. Use at the end of a method to assert that this
        /// method left the world in the promised state.
        /// </summary>
        [Conditional("UNITY_EDITOR"), Conditional("METAFRAME_CONTRACTS")]
        public static void Ensure(bool condition, string reason, Object context = null)
        {
            if (!condition) Fail("Ensure", reason, context, DefaultSeverity);
        }

        // ── Invariant — must always hold while the object is "alive" ─────────

        /// <summary>
        /// Invariant. Use at any point to assert that an internal consistency
        /// rule still holds (e.g. counter == collection.Count).
        /// </summary>
        [Conditional("UNITY_EDITOR"), Conditional("METAFRAME_CONTRACTS")]
        public static void Invariant(bool condition, string reason, Object context = null)
        {
            if (!condition) Fail("Invariant", reason, context, DefaultSeverity);
        }

        // ── Healed — check + repair, always continue ─────────────────────────

        /// <summary>
        /// Self-heal entry point. Evaluates <paramref name="condition"/>; if it
        /// fails, runs <paramref name="repair"/> and logs the repair as a
        /// warning. Returns true if the condition was already met (no repair
        /// needed), false if a repair was performed.
        ///
        /// Unlike Require/Ensure/Invariant, Healed() runs in ALL builds — the
        /// repair is potentially load-bearing. The check is meant to be cheap
        /// (a counter compare, a null check). If it isn't, gate the call site.
        /// </summary>
        public static bool Healed(Func<bool> condition, Action repair, string reason, Object context = null)
        {
            if (condition == null || repair == null) return true;

            bool ok;
            try { ok = condition(); }
            catch (Exception e)
            {
                LogViolation("Healed.condition", $"{reason} (check threw: {e.Message})", context, ContractSeverity.Warn);
                return false;
            }

            if (ok) return true;

            try
            {
                repair();
                TotalHealed++;
                AnyViolations = true;
                Debug.LogWarning(
                    $"[Contract] HEALED — {reason}\n" +
                    $"   context: {ContextLabel(context)}",
                    context);
                return false;
            }
            catch (Exception e)
            {
                LogViolation("Healed.repair", $"{reason} (repair threw: {e.Message})", context, ContractSeverity.Error);
                return false;
            }
        }

        // ── Internal ─────────────────────────────────────────────────────────

        private static void Fail(string kind, string reason, Object context, ContractSeverity severity)
        {
            LogViolation(kind, reason, context, severity);

            if (severity == ContractSeverity.Throw)
                throw new ContractViolationException(
                    $"[Contract.{kind}] {reason} (context: {ContextLabel(context)})");
        }

        private static void LogViolation(string kind, string reason, Object context, ContractSeverity severity)
        {
            TotalViolations++;
            AnyViolations = true;

            string msg =
                $"[Contract] {kind} VIOLATION — {reason}\n" +
                $"   context: {ContextLabel(context)}";

            switch (severity)
            {
                case ContractSeverity.Warn:  Debug.LogWarning(msg, context); break;
                case ContractSeverity.Error: Debug.LogError  (msg, context); break;
                case ContractSeverity.Throw: Debug.LogError  (msg, context); break;
            }
        }

        private static string ContextLabel(Object context)
        {
            if (context == null) return "<null>";
            // Avoid Object.name when the object has been destroyed —
            // throws MissingReferenceException.
            try { return context.name; }
            catch { return context.GetType().Name; }
        }
    }
}
