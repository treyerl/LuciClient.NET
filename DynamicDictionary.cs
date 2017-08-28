using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Luci
{
    public class DynamicDictionary : DynamicObject, IEnumerable<KeyValuePair<string, object>>, IDictionary<string, object>
    {
        Dictionary<string, object> dictionary = new Dictionary<string, object>();
        public int Count
        {
            get
            {
                return dictionary.Count;
            }
        }

        ICollection<string> IDictionary<string, object>.Keys
        {
            get
            {
                return ((IDictionary<string, object>)dictionary).Keys;
            }
        }

        public ICollection<object> Values
        {
            get
            {
                return ((IDictionary<string, object>)dictionary).Values;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return ((IDictionary<string, object>)dictionary).IsReadOnly;
            }
        }

        public object this[string key]
        {
            get
            {
                return ((IDictionary<string, object>)dictionary)[key];
            }

            set
            {
                ((IDictionary<string, object>)dictionary)[key] = value;
            }
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            string name = binder.Name;
            dictionary.TryGetValue(name, out result);
            // don't throw an exception if key does not exist, just return null
            return true;
        }
        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            dictionary[binder.Name] = value;
            return true;
        }

        public IEnumerable<String> Keys()
        {
            return dictionary.Keys.AsEnumerable();
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<string, object>>)dictionary).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<string, object>>)dictionary).GetEnumerator();
        }

        public bool ContainsKey(string key)
        {
            return ((IDictionary<string, object>)dictionary).ContainsKey(key);
        }

        public void Add(string key, object value)
        {
            ((IDictionary<string, object>)dictionary).Add(key, value);
        }

        public bool Remove(string key)
        {
            return ((IDictionary<string, object>)dictionary).Remove(key);
        }

        public bool TryGetValue(string key, out object value)
        {
            return ((IDictionary<string, object>)dictionary).TryGetValue(key, out value);
        }

        public void Add(KeyValuePair<string, object> item)
        {
            ((IDictionary<string, object>)dictionary).Add(item);
        }

        public void Clear()
        {
            ((IDictionary<string, object>)dictionary).Clear();
        }

        public bool Contains(KeyValuePair<string, object> item)
        {
            return ((IDictionary<string, object>)dictionary).Contains(item);
        }

        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            ((IDictionary<string, object>)dictionary).CopyTo(array, arrayIndex);
        }

        public bool Remove(KeyValuePair<string, object> item)
        {
            return ((IDictionary<string, object>)dictionary).Remove(item);
        }
        public override string ToString()
        {
            return dictionary.ToString();
        }
    }
}
