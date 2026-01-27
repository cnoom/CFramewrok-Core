using System;
using System.Collections.Generic;
using UnityEngine;

namespace CFramework.Core
{
    /// <summary>
    /// 可序列化的字典，用于 Unity Inspector 中编辑字典类型数据
    /// </summary>
    [Serializable]
    public class SerializableDictionary<TKey, TValue> : ISerializationCallbackReceiver
    {
        [SerializeField] private List<KeyValuePair> _list = new();

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

        private Dictionary<TKey, TValue> _dictionary = new Dictionary<TKey, TValue>();

        public Dictionary<TKey, TValue> Dictionary => _dictionary;

        public TValue this[TKey key]
        {
            get => _dictionary[key];
            set => _dictionary[key] = value;
        }

        public int Count => _dictionary.Count;
        public ICollection<TKey> Keys => _dictionary.Keys;
        public ICollection<TValue> Values => _dictionary.Values;

        public bool ContainsKey(TKey key) => _dictionary.ContainsKey(key);
        public bool TryGetValue(TKey key, out TValue value) => _dictionary.TryGetValue(key, out value);
        public bool TryGetValue(TKey key, TValue defaultValue) => _dictionary.TryGetValue(key, out defaultValue);
        public void Add(TKey key, TValue value) => _dictionary.Add(key, value);
        public void Remove(TKey key) => _dictionary.Remove(key);
        public void Clear() => _dictionary.Clear();

        public void OnBeforeSerialize()
        {
            _list.Clear();
            foreach (var kvp in _dictionary)
            {
                _list.Add(new KeyValuePair(kvp.Key, kvp.Value));
            }
        }

        public void OnAfterDeserialize()
        {
            _dictionary.Clear();
            foreach (var kvp in _list)
            {
                if (!_dictionary.ContainsKey(kvp.key))
                {
                    _dictionary[kvp.key] = kvp.value;
                }
            }
        }
    }
}