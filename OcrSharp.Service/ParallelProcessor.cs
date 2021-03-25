using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace OcrSharp.Service
{
    public class ParallelProcessor<T>
    {
        SlicedList<T>[] listSlices;
        int numberOfThreads;
        Action<T> action;
        ManualResetEvent[] manualResetEvents;

        public ParallelProcessor(int NumberOfThreads, Action<T> Action)
        {
            this.numberOfThreads = NumberOfThreads;
            this.listSlices = new SlicedList<T>[numberOfThreads];
            this.action = Action;
            this.manualResetEvents = new ManualResetEvent[numberOfThreads];

            for (int i = 0; i < numberOfThreads; i++)
            {
                listSlices[i] = new SlicedList<T>();
                manualResetEvents[i] = new ManualResetEvent(false);
                listSlices[i].indexes = new LinkedList<int>();
                listSlices[i].manualResetEvent = manualResetEvents[i];
            }
        }

        public void ForEach(IEnumerable<T> Items)
        {
            prepareListSlices(Items);
            for (int i = 0; i < numberOfThreads; i++)
            {
                manualResetEvents[i].Reset();
                ThreadPool.QueueUserWorkItem(new WaitCallback(
                    DoWork), listSlices[i]);
            }
            WaitHandle.WaitAll(manualResetEvents);
        }

        private void prepareListSlices(IEnumerable<T> items)
        {
            for (int i = 0; i < numberOfThreads; i++)
            {
                listSlices[i].items = items.ToList();
                listSlices[i].indexes.Clear();
            }
            for (int i = 0; i < items.ToList().Count; i++)
            {
                listSlices[i % numberOfThreads].indexes.AddLast(i);
            }
        }

        private void DoWork(object o)
        {
            SlicedList<T> slicedList = (SlicedList<T>)o;

            foreach (int i in slicedList.indexes)
            {
                action(slicedList.items[i]);
            }
            slicedList.manualResetEvent.Set();
        }
    }

    public class SlicedList<T>
    {
        public List<T> items;
        public LinkedList<int> indexes;
        public ManualResetEvent manualResetEvent;
    }
}
