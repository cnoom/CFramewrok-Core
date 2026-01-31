using System;
using System.Collections.Generic;
using UnityEngine;

namespace CFramework.Core
{
    /// <summary>
    ///     可序列化的字典，用于 Unity Inspector 中编辑字典类型数据
    /// </summary>
    [Serializable]
    public class SerializableDictionary<TKey, TValue> : ISerializationCallbackReceiver
    {
        [SerializeField]
        private List<KeyValuePair> _list = new List<KeyValuePair>();

        public Dictionary<TKey, TValue> Dictionary { get; } = new Dictionary<TKey, TValue>();

        public TValue this[TKey key]
        {
            get => Dictionary[key];
            set => Dictionary[key] = value;
        }

        public int Count => Dictionary.Count;
        public ICollection<TKey> Keys => Dictionary.Keys;
        public ICollection<TValue> Values => Dictionary.Values;

        public void OnBeforeSerialize()
        {
            _list.Clear();
            foreach (KeyValuePair<TKey, TValue> kvp in Dictionary)
            {
                _list.Add(new KeyValuePair(kvp.Key, kvp.Value));
            }
        }

        public void OnAfterDeserialize()
        {
            Dictionary.Clear();
            foreach (KeyValuePair kvp in _list)
            {
                if(!Dictionary.ContainsKey(kvp.key))
                {
                    Dictionary[kvp.key] = kvp.value;
                }
            }
        }

        public bool ContainsKey(TKey key)
        {
            return Dictionary.ContainsKey(key);
        }
        public bool TryGetValue(TKey key, out TValue value)
        {
            return Dictionary.TryGetValue(key, out value);
        }
        public bool TryGetValue(TKey key, TValue defaultValue)
        {
            return Dictionary.TryGetValue(key, out defaultValue);
        }
        public void Add(TKey key, TValue value)
        {
            Dictionary.Add(key, value);
        }
        public void Remove(TKey key)
        {
            Dictionary.Remove(key);
        }
        public void Clear()
        {
            Dictionary.Clear();
        }

        [Serializable]
        public struct KeyValuePair
        {
            public TKey key;
            public TValue value;

            public KeyValuePair(TKey key, TValue value)
            {
                this.key = key;
                this.value = value;
            }
        }
    }
}