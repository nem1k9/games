using System;
using System.Collections;
using UnityEngine;

namespace Gnomes
{
    /// <summary>Global coroutine runner for plain C# code (delayed actions).</summary>
    public class Mono : MonoBehaviour
    {
        static Mono inst;

        static Mono Inst
        {
            get
            {
                if (inst == null)
                {
                    var go = new GameObject("MonoRunner");
                    DontDestroyOnLoad(go);
                    inst = go.AddComponent<Mono>();
                }
                return inst;
            }
        }

        public static void Delay(float seconds, Action a) => Inst.StartCoroutine(Run(seconds, a));

        static IEnumerator Run(float s, Action a)
        {
            yield return new WaitForSeconds(s);
            try { a(); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
}
