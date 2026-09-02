using System.Collections.Generic;
using UnityEngine;

namespace Bunker.AI
{
    /// <summary>
    /// Zombi havuzu. M1-05.
    ///
    /// <para><b>Neden havuz:</b> tur 20'de yüzlerce zombi doğup ölür.
    /// <c>Instantiate</c>/<c>Destroy</c> çifti her seferinde tahsis ve çöp toplama
    /// demektir; çöp toplayıcının durduğu kare, oyuncunun "takıldı" dediği karedir.</para>
    ///
    /// <para><b>Sınırlıdır ve sınırda ne olacağı tanımlıdır</b> (systems-code.md):
    /// havuz doluyken <see cref="Rent"/> <c>null</c> döner. Sessizce büyüyen bir havuz,
    /// ertelenmiş bir kare hızı hatasıdır — sınır PERF-BUDGET'tan gelir, tahminden
    /// değil.</para>
    ///
    /// <para><b>Sıfırlama sözleşmesi:</b> havuzdan çıkan zombi her seferinde
    /// <c>ZombieAgent.Spawn</c> ile kurulur. Önceki hayatından can, durum ya da hedef
    /// taşıyan bir nesne, bir saat oynadıktan sonra ortaya çıkan türden bir hatadır.</para>
    /// </summary>
    public sealed class ZombiePool
    {
        private readonly ZombieAgent _prefab;
        private readonly Transform _parent;
        private readonly Stack<ZombieAgent> _idle;
        private readonly int _capacity;

        private int _created;

        public ZombiePool(ZombieAgent prefab, Transform parent, int capacity)
        {
            _prefab = prefab != null ? prefab : throw new System.ArgumentNullException(nameof(prefab));
            _parent = parent;
            _capacity = capacity < 1 ? 1 : capacity;
            _idle = new Stack<ZombieAgent>(_capacity);
        }

        /// <summary>Şu an sahada olan zombi sayısı.</summary>
        public int ActiveCount => _created - _idle.Count;

        /// <summary>Üst sınır. PERF-BUDGET'tan gelir (`rounds.json` → `count.maxConcurrent`).</summary>
        public int Capacity => _capacity;

        /// <summary>Havuzda yer var mı.</summary>
        public bool HasRoom => ActiveCount < _capacity;

        /// <summary>
        /// Havuzu önceden doldurur. İlk turun ilk saniyesinde 6 nesne birden
        /// yaratmak, oyunun ilk izlenimini bir takılmayla açar.
        /// </summary>
        public void Prewarm(int count)
        {
            for (int i = 0; i < count && _created < _capacity; i++)
            {
                ZombieAgent zombie = Create();
                zombie.gameObject.SetActive(false);
                _idle.Push(zombie);
            }
        }

        /// <summary>
        /// Havuzdan bir zombi verir. <b>Havuz doluysa <c>null</c> döner</b> — çağıran
        /// taraf bunu bir hata değil, tanımlı bir durum olarak ele almalı.
        /// </summary>
        public ZombieAgent Rent()
        {
            if (!HasRoom) return null;

            ZombieAgent zombie = _idle.Count > 0 ? _idle.Pop() : Create();
            zombie.gameObject.SetActive(true);
            return zombie;
        }

        /// <summary>Zombiyi havuza iade eder. Zaten iade edilmişse hiçbir şey yapmaz.</summary>
        public void Return(ZombieAgent zombie)
        {
            if (zombie == null) return;
            if (_idle.Contains(zombie)) return;

            zombie.gameObject.SetActive(false);
            _idle.Push(zombie);
        }

        /// <summary>Havuzu tamamen boşaltır — sahne geçişinde.</summary>
        public void DestroyAll()
        {
            while (_idle.Count > 0)
            {
                ZombieAgent zombie = _idle.Pop();
                if (zombie != null) Object.Destroy(zombie.gameObject);
            }

            _created = 0;
        }

        private ZombieAgent Create()
        {
            ZombieAgent zombie = Object.Instantiate(_prefab, _parent);
            zombie.name = $"Zombie_{_created:D3}";
            _created++;
            return zombie;
        }
    }
}
