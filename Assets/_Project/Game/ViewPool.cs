using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES.Game
{
    /// <summary>Reaproveita componentes visuais (ativa/desativa em vez de criar e destruir).</summary>
    public sealed class ViewPool<T> where T : Component
    {
        readonly Func<T> _create;
        readonly Stack<T> _free = new Stack<T>(64);

        public int Created { get; private set; }
        public int FreeCount => _free.Count;

        public ViewPool(Func<T> create) { _create = create; }

        public T Get()
        {
            T v;
            if (_free.Count > 0) v = _free.Pop();
            else { v = _create(); Created++; }
            v.gameObject.SetActive(true);
            return v;
        }

        public void Release(T v)
        {
            if (v == null) return;
            v.gameObject.SetActive(false);
            _free.Push(v);
        }
    }
}
