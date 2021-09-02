using Microsoft.AspNetCore.Components;
using ObservableCollections;

namespace BlazorApp.Pages;

public partial class Index
{
    ObservableList<int> list;
    public ISynchronizedView<int, int> ItemsView { get; set; }
    int adder = 99;


    RenderFragment fragment;

    protected override void OnInitialized()
    {
        list = new ObservableList<int>();
        list.AddRange(new[] { 1, 10, 188 });
        ItemsView = list.CreateSortedView(x => x, x => x, comparer: Comparer<int>.Default).WithINotifyCollectionChanged();


        fragment = builder =>
        {
            builder.GetFrames();

        };
    }

    

    void OnClick()
    {
        ThreadPool.QueueUserWorkItem(_ =>
        {
            list.Add(adder++);

            _ = InvokeAsync(StateHasChanged);
        });
    }
}
