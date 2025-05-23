using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace JetBrains.Rider.Unity.Editor.Utils
{
  internal class BoundedSynchronizedQueue<T> where T : class
  {
    private readonly int myMaxSize;
    private readonly Queue<T> myQueue;
    private readonly object myLockObject = new();

    public BoundedSynchronizedQueue(int maxSize)
    {
      if (maxSize < 0)
        throw new ArgumentOutOfRangeException(nameof (maxSize), "ArgumentOutOfRange_NeedNonNegNum");

      myMaxSize = maxSize;
      myQueue = new Queue<T>();
    }

    [CanBeNull]
    public T Dequeue()
    {
      lock (myLockObject)
      {
        return myQueue.Count > 0 ? myQueue.Dequeue() : null;
      }
    }

    public void Enqueue([NotNull]T input)
    {
      if (input == null)
        throw new ArgumentNullException("input is null.");

      lock (myLockObject)
      {
        myQueue.Enqueue(input);
        if (myQueue.Count >= myMaxSize)
          myQueue.Dequeue(); // limit max size
      }
    }
  }
}