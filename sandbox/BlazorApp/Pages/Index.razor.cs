using Microsoft.AspNetCore.Components;
using ObservableCollections;

namespace BlazorApp.Pages;

public partial class Index : IDisposable
{
    ObservableList<int> list;
    public ISynchronizedView<int, int> ItemsView { get; set; }
    int adder = 99;

    protected override void OnInitialized()
    {
        list = new ObservableList<int>();
        ItemsView = list.CreateView(x => x);

        ItemsView.CollectionStateChanged += action =>
        {
            InvokeAsync(StateHasChanged);
        };
    }

    public void Dispose()
    {
        ItemsView.Dispose();
    }




    void OnClick()
    {
        ThreadPool.QueueUserWorkItem(_ =>
        {
            list.Add(adder++);
        });
    }
}
