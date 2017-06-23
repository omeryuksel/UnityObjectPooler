// Author : Ömer Yüksel
// https://github.com/omeryuksel

using Object = UnityEngine.Object;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Collections;

namespace Nova.ObjectPooling
{
    //Add this component to object that you want to pool,
    //If you don't,it will be added automatically
    public abstract class PoolableBehaviour : MonoBehaviour
    {
        public ObjectPooler ownerPool;
        public virtual void OnBorn() { }
        public virtual void OnReborn() { }
        public virtual void OnRelease() { }

        public void Release()
        {
            ownerPool.Release(this.gameObject);
        }
    }

    public class ObjectPooler : MonoBehaviour
    {
        [SerializeField]
        private bool CrossScenePool = false;

        private void Awake()
        {
            if (CrossScenePool) DontDestroyOnLoad(this);
        }

        private partial class GenericObjectPool
        {
            public List<GameObject> waitingObjects = new List<GameObject>();
            public List<GameObject> liveObjects = new List<GameObject>();
            public void AddLiveObject(GameObject instance)
            {
                liveObjects.Add(instance);
            }
        }

        private Dictionary<string, GenericObjectPool> pools = new Dictionary<string, GenericObjectPool>();

        public void PopulateAsync(GameObject original, int count, Action onComplete, float interval)
        {
            StartCoroutine(PopulateAsyncCoroutine(original, count, interval, onComplete));
        }

        private IEnumerator PopulateAsyncCoroutine(GameObject original, int count, float interval, Action onComplete)
        {
            for (int i = 0; i < count; i++)
            {
                ForceInstantiate(original);
                yield return new WaitForSeconds(interval);
            }
            onComplete();
        }

        public void Populate(GameObject original, int count)
        {
            for (int i = 0; i < count; i++)
            {
                ForceInstantiate(original);
            }
        }

        private void ForceInstantiate(GameObject original)
        {
            if (!pools.ContainsKey(original.tag)) CreateNewPool(original.tag);
            var instance = CreateObject(original);
            pools[original.tag].waitingObjects.Add(instance);
            instance.SetActive(false);
            instance.transform.parent = transform;
        }

        public void Release(GameObject clone)
        {
            if (pools.ContainsKey(clone.tag))
            {
                var targetPool = pools[clone.tag];
                targetPool.liveObjects.Remove(clone);
                targetPool.waitingObjects.Add(clone);
                clone.SetActive(false);
            }

            clone.GetComponent<PoolableBehaviour>().OnRelease();
        }

        public GameObject Instantiate(GameObject original, Transform parent)
        {
            var instance = Instantiate(original);
            instance.transform.parent = parent;
            instance.transform.position = Vector3.zero;
            return instance;
        }

        public GameObject Instantiate(GameObject original)
        {
            GameObject instance = null;
            if (pools.ContainsKey(original.tag))
            {
                var targetPool = pools[original.tag];
                if (targetPool.waitingObjects.Count > 0)
                {
                    //Idle object available in pool
                    instance = targetPool.waitingObjects[0];
                    targetPool.waitingObjects.Remove(instance);
                    targetPool.liveObjects.Add(instance);
                }
                else
                {   // Not enough items in pool
                    instance = CreateObject(original);
                    targetPool.liveObjects.Add(instance);
                }
            }
            else
            {   // There is no pool available
                instance = CreateObject(original);
                var newPool = CreateNewPool(original.tag);
                newPool.AddLiveObject(instance);
            }

            instance.transform.parent = transform;
            instance.SetActive(true);

            var pb = instance.GetComponent<PoolableBehaviour>();
            if (pb == null) pb = instance.AddComponent<PoolableBehaviour>();
            pb.OnReborn();

            return instance;
        }


        private GenericObjectPool CreateNewPool(string tag)
        {
            var newPool = new GenericObjectPool();
            pools.Add(tag, newPool);
            return newPool;
        }

        private GameObject CreateObject(GameObject original)
        {
            var instance = Object.Instantiate(original);
            instance.GetComponent<PoolableBehaviour>().ownerPool = this;
            return instance;
        }
    }
}
