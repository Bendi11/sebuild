
namespace SeBuild.Pass.Rename;

/// <summary>
/// Wrapper over a <c>Dictionary</c> that allows for key to multi-value mappings
/// </summary>
class MultiMap<K, V>: IEnumerable<KeyValuePair<K, HashSet<V>>>
    where K: notnull {
        Dictionary<K, HashSet<V>> _multiMap;
        
        /// <summary>
        /// Create a new <c>MultiMap</c> using the given <paramref name="cmp"/> to compare keys of type <typeparamref name="K"/>
        /// for equality
        /// </summary>
        public MultiMap(System.Collections.Generic.IEqualityComparer<K>? cmp = null) {
            _multiMap = 
                new Dictionary<K, HashSet<V>>(cmp);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        
        public IEnumerator<KeyValuePair<K, HashSet<V>>> GetEnumerator() => _multiMap.GetEnumerator();
        
        /// <summary>
        /// Check if the given <paramref name="key"/> is mapped to one or more values
        /// </summary>
        public bool Contains(K key) => _multiMap.ContainsKey(key);
    
        /// <summary>
        /// Add a value of type <typeparamref name="V"/> to the list of values for the given <paramref name="key"/>
        /// </summary>
        public void Add(K key, V value) {
            HashSet<V>? references = null;
            if(_multiMap.TryGetValue(key, out references)) {
                references.Add(value);
            } else {
                references = new HashSet<V>();
                references.Add(value);
                _multiMap[key] = references;
            }
        }
        
        /// <summary>
        /// Remove all values for the given <paramref name="key"/>
        /// </summary>
        public void Clear(K key) => _multiMap[key] = new HashSet<V>();
        
        /// <summary>
        /// Map the <paramref name="from"/> key to track changes to the <paramref name="to"/> key's list.
        /// This is implemented as setting the <paramref name="from"/> key's list of values to the same instance as
        /// the <paramref name="to"/> key.
        /// </summary>
        public void Map(K from, K to) => _multiMap[from] = _multiMap[to];
        
        /// <summary>
        /// Retrieve the set of values associated with the given <paramref name="key"/>
        /// </summary>
        public HashSet<V>? Get(K key) {
            HashSet<V>? value = null;
            _multiMap.TryGetValue(key, out value);
            return value;
        }
    }
