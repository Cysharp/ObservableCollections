using ObservableCollections;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SampleScript : MonoBehaviour
{
    public Button prefab;
    public GameObject root;

    public Button add;
    public Button remove;

    int i = 0;

    void Start()
    {
        var oc = new ObservableRingBuffer<int>();

        var view = oc.CreateView(x =>
        {
            var item = GameObject.Instantiate(prefab);
            item.GetComponentInChildren<Text>().text = x.ToString();
            return item.gameObject;
        });
        view.AttachFilter(new GameObjectFilter(root));

        add.onClick.AddListener(() =>
        {
            oc.AddLast(i++);
        });

        remove.onClick.AddListener(() =>
        {
            oc.RemoveFirst();
        });

    }

}

public class GameObjectFilter : ISynchronizedViewFilter<int, GameObject>
{
    readonly GameObject root;

    public GameObjectFilter(GameObject root)
    {
        this.root = root;
    }

    public void OnCollectionChanged(ChangedKind changedKind, int value, GameObject view, in NotifyCollectionChangedEventArgs<int> eventArgs)
    {
        if (changedKind == ChangedKind.Add)
        {
            view.transform.SetParent(root.transform);
        }
        else if (changedKind == ChangedKind.Remove)
        {
            GameObject.Destroy(view);
        }
    }

    public bool IsMatch(int value, GameObject view)
    {
        return value % 2 == 0;
    }

    public void WhenTrue(int value, GameObject view)
    {
        view.SetActive(true);
    }

    public void WhenFalse(int value, GameObject view)
    {
        view.SetActive(false);
    }
}
