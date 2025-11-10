using UnityEngine;

public class MoneyManager : MonoBehaviour
{
    public static MoneyManager I;
    public int Money;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
    }

    public void Add(int amount)
    {
        Money += amount;
        Debug.Log("Money: " + Money);
    }
}
