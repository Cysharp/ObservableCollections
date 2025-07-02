using System;
using System.Linq;
using System.Threading.Tasks;
using ObservableCollections;
using R3;

namespace WinUI3App;

public class SampleService
{
    public ObservableList<Item> Items { get; set; } = [];
    private int _index;
    private readonly Random _random = new();

    public SampleService()
    {
        Observable.Interval(TimeSpan.FromSeconds(1)).SubscribeAwait(async (_, _) =>
        {
            // Simulate a delay to mimic data fetching or processing
            await Task.Delay(10);

            var count = _random.Next(-3, 5);

            switch (count)
            {
                case > 0:
                    {
                        for (var i = 0; i < count; i++)
                        {
                            _index++;
                            Items.Add(new Item
                            {
                                Id = _index,
                                Name = $"Item {_index}",
                                Status = true
                            });
                        }

                        break;
                    }
                case < 0:
                    {
                        for (var i = 0; i < -count && Items.Count > 0; i++)
                        {
                            var item = Items[_random.Next(0, Items.Count)];
                            item.Status = false;
                        }

                        break;
                    }
            }

            foreach (var removed in Items.Where(x => x.Status == false).ToArray())
            {
                // ICollectionEventDispatcher.Postで、synchronizationContext.Postを呼び出している場合、ここで落ちる
                Items.Remove(removed);
            }
        });
    }
}

public class Item
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Status { get; set; }
}
