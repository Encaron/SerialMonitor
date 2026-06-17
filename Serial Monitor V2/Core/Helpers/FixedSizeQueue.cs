using System.Collections.Generic;
using System.Linq;

namespace 串口助手
{
    /// <summary>
    /// 定长队列：Enqueue 后保持容量不超过 capacity，旧数据自动丢弃。
    /// 用于传感卡片迷你波形 History（30 点）。
    /// </summary>
    public class FixedSizeQueue<T>
    {
        private readonly Queue<T> _queue = new();
        private readonly int _capacity;

        public FixedSizeQueue(int capacity)
        {
            _capacity = capacity;
        }

        public void Enqueue(T item)
        {
            _queue.Enqueue(item);
            while (_queue.Count > _capacity)
                _queue.Dequeue();
        }

        public List<T> GetSnapshot() => _queue.ToList();
        public int Count => _queue.Count;
    }
}
