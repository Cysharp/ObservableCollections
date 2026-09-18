using System;

namespace ObservableCollections.Tests;

/// <summary>
/// 書き込み可能ビューの位置ベース操作 (セッター、RemoveAt、Insert) は、View インデックスをソース
/// インデックスへ逆引き (AlternateIndexList.GetAlternateIndex) してからソースに反映しなければならない。
///
/// T と TView を別の型にして、converter をスキップするショートカット
/// (typeof(T) == typeof(TView)) と絡まないようにしている。
/// </summary>
public class WritableViewIndexTranslationTest
{
    static int ToOriginal(string newView, int original, ref bool setValue)
    {
        return int.Parse(newView.Substring(1));
    }

    [Fact]
    public void RemoveAt()
    {
        var list = new ObservableList<int>();
        list.Add(1);
        list.Add(2);
        list.Add(3);
        list.Add(4);

        using var view = list.CreateWritableView(x => $"${x}");
        view.AttachFilter(x => x % 2 == 0);

        using var bindable = view.ToWritableNotifyCollectionChanged(ToOriginal);

        // View は ["$2", "$4"]。その [1] は "$4" なので、ソースからは 4 が消えるべき。
        bindable.RemoveAt(1);

        list.Should().Equal(new[] { 1, 2, 3 });

        bindable.Count.Should().Be(1);
        bindable[0].Should().Be("$2");
    }

    [Fact]
    public void Insert()
    {
        var list = new ObservableList<int>();
        list.Add(1);
        list.Add(2);
        list.Add(3);
        list.Add(4);

        using var view = list.CreateWritableView(x => $"${x}");
        view.AttachFilter(x => x % 2 == 0);

        using var bindable = view.ToWritableNotifyCollectionChanged(ToOriginal);

        // View は ["$2", "$4"]。その [1] の位置に入れたいので、
        // ソースでは 4 (ソース インデックス 3) の直前に入るべき。
        bindable.Insert(1, "$6");

        list.Should().Equal(new[] { 1, 2, 3, 6, 4 });

        bindable.Count.Should().Be(3);
        bindable[0].Should().Be("$2");
        bindable[1].Should().Be("$6");
        bindable[2].Should().Be("$4");
    }

    /// <summary>
    /// 末尾への挿入はソース インデックスが決まらないので末尾追加として扱われる。
    /// これは正当な操作なので、範囲外の拒否と混同してはならない。
    /// </summary>
    [Fact]
    public void InsertAtTail()
    {
        var list = new ObservableList<int>();
        list.Add(1);
        list.Add(2);
        list.Add(3);
        list.Add(4);

        using var view = list.CreateWritableView(x => $"${x}");
        view.AttachFilter(x => x % 2 == 0);

        using var bindable = view.ToWritableNotifyCollectionChanged(ToOriginal);

        // View は ["$2", "$4"]。その [2] は末尾。
        bindable.Insert(2, "$6");

        list.Should().Equal(new[] { 1, 2, 3, 4, 6 });

        bindable.Should().Equal(new[] { "$2", "$4", "$6" });
    }

    /// <summary>
    /// 範囲外のインデックスは ArgumentOutOfRangeException で拒否し、ソースを変更しないことを確認する。
    /// 末尾追加へのフォールバックが範囲外まで飲み込んでしまっていた。
    /// </summary>
    [Fact]
    public void OutOfRangeIndexIsRejected()
    {
        var list = new ObservableList<int>();
        list.Add(1);
        list.Add(2);
        list.Add(3);
        list.Add(4);

        using var view = list.CreateWritableView(x => $"${x}");
        view.AttachFilter(x => x % 2 == 0);

        using var bindable = view.ToWritableNotifyCollectionChanged(ToOriginal);

        // View は ["$2", "$4"] なので Count は 2。
        bindable.Invoking(x => x.Insert(3, "$6")).Should().Throw<ArgumentOutOfRangeException>();
        bindable.Invoking(x => x.Insert(-1, "$6")).Should().Throw<ArgumentOutOfRangeException>();
        bindable.Invoking(x => x.RemoveAt(2)).Should().Throw<ArgumentOutOfRangeException>();
        bindable.Invoking(x => x.RemoveAt(-1)).Should().Throw<ArgumentOutOfRangeException>();
        bindable.Invoking(x => x[2] = "$6").Should().Throw<ArgumentOutOfRangeException>();
        bindable.Invoking(x => x[-1] = "$6").Should().Throw<ArgumentOutOfRangeException>();

        list.Should().Equal(new[] { 1, 2, 3, 4 });
    }
}
