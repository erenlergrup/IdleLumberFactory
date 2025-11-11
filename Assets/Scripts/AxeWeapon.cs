using UnityEngine;
using System.Collections.Generic;

public class AxeWeapon : MonoBehaviour
{
    [Header("Hitbox")]
    public Collider hitbox;                 // IsTrigger collider
    public int damage = 1;
    public LayerMask treeMask;

    bool active;                            // bu salınımda hasar açık mı?
    HashSet<Tree> hitThisSwing = new();     // tek salınımda aynı ağacı 1 kere say

    void Reset() { if (hitbox == null) hitbox = GetComponent<Collider>(); }

    // === Animation Event'lerinden çağrılacak ===
    public void SwingStart()
    {
        active = true;
        hitThisSwing.Clear();
        if (hitbox) hitbox.enabled = true;
    }

    public void SwingEnd()
    {
        active = false;
        if (hitbox) hitbox.enabled = false;
        hitThisSwing.Clear();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!active) return;
        if (((1 << other.gameObject.layer) & treeMask) == 0) return;

        var tree = other.GetComponentInParent<Tree>();
        if (tree && !hitThisSwing.Contains(tree))
        {
            tree.Hit(damage);
            hitThisSwing.Add(tree);
            // TODO: parçacık/ses koyabilirsin
        }
    }
}
