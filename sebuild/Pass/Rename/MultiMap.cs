
namespace SeBuild.Pass.Rename;

class MultiMap<K, V>: IEnumerable<KeyValuePair<K, HashSet<V>>>
    where K: notnull {
        Dictionary<K, HashSet<V>> _multiMap;


        public MultiMap(System.Collections.Generic.IEqualityComparer<K>? cmp = null) {
            _multiMap = 
                new Dictionary<K, HashSet<V>>(cmp);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        public IEnumerator<KeyValuePair<K, HashSet<V>>> GetEnumerator() =>
            _multiMap.GetEnumerator();

        public bool Contains(K symbol) =>
            _multiMap.ContainsKey(symbol);

        public void Add(K symbol, V reference) {
            HashSet<V>? references = null;
            if(_multiMap.TryGetValue(symbol, out references)) {
                references.Add(reference);
            } else {
                references = new HashSet<V>();
                references.Add(reference);
                _multiMap[symbol] = references;
            }
        }
        
        /// Remove all references for the given symbol, without removing it from the map
        public void Clear(K symbol) =>
            _multiMap[symbol] = new HashSet<V>();
        
        /// Remap the `from` key to track changes to the `to` key's list
        public void Remap(K from, K to) => _multiMap[from] = _multiMap[to];

        public HashSet<V>? Get(K symbol) {
            HashSet<V>? value = null;
            _multiMap.TryGetValue(symbol, out value);
            return value;
        }
    }
