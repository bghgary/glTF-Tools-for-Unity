using System;
using System.Collections.Generic;

public class ObjectTracker : IDisposable
{
    private List<UnityEngine.Object> objects = new List<UnityEngine.Object>();

    public void Dispose()
    {
        this.objects.ForEach(material => UnityEngine.Object.DestroyImmediate(material));
    }

    public T Add<T>(T obj) where T : UnityEngine.Object
    {
        this.objects.Add(obj);
        return obj;
    }
}
