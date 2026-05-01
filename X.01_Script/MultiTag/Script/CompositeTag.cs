#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MetaFrame.Tags
{
	[CreateAssetMenu(menuName = "MetaFrame/Tags/Composite Tag")]
#if ODIN_INSPECTOR
	[AssetSelector, Required]
#endif
	public sealed class CompositeTag : ScriptableObject
	{
		[SerializeField] private Tag[] _tags = Array.Empty<Tag>();

		internal IEnumerable<Tag> Tags => _tags;

		// FIX (T4-1): null-guard each tag element. If a Tag asset referenced by
		// _tags is deleted, the array slot becomes null but the array length is
		// preserved. Without the guard, every Add/Remove on this CompositeTag
		// would NRE on the missing slot and break the whole operation.
		internal void Add(GameObject instance, int hash)
		{
			for (int i = 0; i < _tags.Length; i++)
				if (_tags[i] != null) _tags[i].Add(instance, hash);
		}

		internal void Remove(GameObject instance, int hash)
		{
			for (int i = 0; i < _tags.Length; i++)
				if (_tags[i] != null) _tags[i].Remove(instance, hash);
		}

		internal bool HasInstance(GameObject instance, bool allRequired)
		{
			return instance.HasTags(_tags, allRequired);
		}
	}
}
