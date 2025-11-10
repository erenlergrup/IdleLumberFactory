using UnityEngine;

public class SellZone : MonoBehaviour
{
    public int pricePerLog = 5;

    void OnTriggerEnter(Collider other)
    {
        var pc = other.GetComponent<PlayerController>();
        if (pc == null) return;

        var carried = pc.TakeCarried();
        if (carried != null && carried.CompareTag("Log"))
        {
            MoneyManager.I.Add(pricePerLog);
            pc.ClearCarried(); // kütüğü yok eder
        }
    }
}
