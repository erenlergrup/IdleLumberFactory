using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float accelTime = 0.12f;
    public float turnSmoothTime = 0.08f;

    [Header("Interact")]
    public Transform carryPoint;
    public float hitRange = 2f;
    public LayerMask treeMask;

    Rigidbody rb;
    Vector3 inputDir, velRef;
    float turnVelRef;

    GameObject carried;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ; // Y istersen ekle
        rb.angularDamping = 3f;
        rb.maxAngularVelocity = 2f;
    }

    void Update()
    {
        // 1) Input sadece oku
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        inputDir = new Vector3(h, 0, v).normalized;

        // Kes (LMB)
        if (Input.GetMouseButtonDown(0))
        {
            var pos = transform.position + transform.forward * 1.0f;
            var cols = Physics.OverlapSphere(pos, hitRange, treeMask);
            foreach (var c in cols)
            {
                var t = c.GetComponentInParent<Tree>();
                if (t != null) { t.Hit(1); break; }
            }
        }

        // Al/Bırak (E)
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (carried == null) TryPickup();
            else Drop();
        }
    }

    void FixedUpdate()
    {
        // 2) Fiziği burada uygula
        // Hızlandırma / yavaşlatma (smooth)
        Vector3 targetVel = inputDir * moveSpeed;
        Vector3 curVel = rb.linearVelocity;
        Vector3 newVel = new Vector3(
            Mathf.SmoothDamp(curVel.x, targetVel.x, ref velRef.x, accelTime),
            curVel.y,
            Mathf.SmoothDamp(curVel.z, targetVel.z, ref velRef.z, accelTime)
        );

        rb.MovePosition(rb.position + newVel * Time.fixedDeltaTime);

        // 3) Dönüşü yumuşat (sadece hareket varsa)
        if (inputDir.sqrMagnitude > 0.0001f)
        {
            float targetAngle = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnVelRef, turnSmoothTime);
            rb.MoveRotation(Quaternion.Euler(0f, angle, 0f));
        }

        // 4) Çarpışmadan gelen açısal momenti söndür
        rb.angularVelocity = Vector3.zero;
    }

    void TryPickup()
    {
        Collider[] around = Physics.OverlapSphere(transform.position, 2f);
        foreach (var c in around)
        {
            if (c.CompareTag("Log"))
            {
                carried = c.gameObject;
                var rb2 = carried.GetComponent<Rigidbody>();
                if (rb2) rb2.isKinematic = true;

                // Layer’ını LogCarried gibi çarpışmayan bir layer’a al
                carried.layer = LayerMask.NameToLayer("IgnorePlayer");

                carried.transform.SetParent(carryPoint);
                carried.transform.localPosition = Vector3.zero;
                carried.transform.localRotation = Quaternion.identity;
                break;
            }
        }
    }

    public GameObject TakeCarried() => carried;

    public void ClearCarried()
    {
        if (!carried) return;
        Destroy(carried);
        carried = null;
    }

    void Drop()
    {
        if (!carried) return;
        var rb2 = carried.GetComponent<Rigidbody>();
        carried.transform.SetParent(null);
        if (rb2)
        {
            carried.layer = LayerMask.NameToLayer("Log");
            rb2.isKinematic = false;
            rb2.AddForce(transform.forward * 2f, ForceMode.VelocityChange);
        }
        carried = null;
    }
}
