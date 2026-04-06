using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json.Serialization;

namespace Axphi.Data
{
    public class RelationObject<TParent>
        where TParent : class
    {
        [JsonIgnore]
        public TParent? Parent { get; private set; }

        public class Collection<TChild> : ObservableCollection<TChild>
            where TChild : RelationObject<TParent>
        {
            private readonly TParent _parent;

            public Collection(TParent parent)
            {
                if (parent is null)
                {
                    throw new ArgumentNullException(nameof(parent));
                }

                _parent = parent;
            }

            protected override void InsertItem(int index, TChild item)
            {
                if (item is null)
                {
                    throw new ArgumentNullException(nameof(item));
                }

                if (item.Parent is not null)
                {
                    throw new InvalidOperationException("Item already has a parent.");
                }

                item.Parent = _parent;
                base.InsertItem(index, item);
            }

            protected override void RemoveItem(int index)
            {
                var item = this[index];
                item.Parent = null;

                base.RemoveItem(index);
            }

            protected override void SetItem(int index, TChild item)
            {
                if (item.Parent is not null)
                {
                    throw new InvalidOperationException("Item already has a parent.");
                }

                var oldItem = this[index];
                oldItem.Parent = null;

                item.Parent = _parent;

                base.SetItem(index, item);
            }

            protected override void ClearItems()
            {
                foreach (var child in this)
                {
                    child.Parent = null;
                }

                base.ClearItems();
            }
        }
    }
}
