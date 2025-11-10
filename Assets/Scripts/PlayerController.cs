using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float accelTime = 0.12f;        // hız yumuşatma
    public float turnSmoothTime = 0.08f;   // dönüş yumuşatma

    [Header("Interact")]
    public Transform carryPoint;
    public float hitRange = 2f;
    public LayerMask treeMask;

    private Rigidbody rb;
    private Camera cam;

    // smoothing state
    private Vector2 input;                 // Update'ta okunur
    private float currentSpeed;
    private float speedVel;
    private float turnSmoothVel;

    private GameObject carried;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotationX |
                         RigidbodyConstraints.FreezeRotationZ;
        cam = Camera.main;
    }

    void Update()
    {
        // 1) Ham input sadece Update'ta
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        input = new Vector2(h, v);

        // 2) Kes (LMB)
        if (Input.GetMouseButtonDown(0))
        {
            Collider[] cols = Physics.OverlapSphere(transform.position + transform.forward * 1.0f, hitRange, treeMask);
            foreach (var c in cols)
            {
                var t = c.GetComponentInParent<Tree>();
                if (t != null) { t.Hit(1); break; }
            }
        }

        // 3) Al/Bırak (E)
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (carried == null) TryPickup();
            else Drop();
        }
    }

    void FixedUpdate()
    {
        // Kamera yönüne göre dünya ekseninde yön üret
        Vector3 camForward = cam.transform.forward; camForward.y = 0f; camForward.Normalize();
        Vector3 camRight = cam.transform.right;     camRight.y = 0f;   camRight.Normalize();

        Vector3 moveDir = (camForward * input.y + camRight * input.x);
        float inputMag = Mathf.Clamp01(moveDir.magnitude);
        moveDir = inputMag > 0.0001f ? moveDir.normalized : Vector3.zero;

        // Hız yumuşatma
        float targetSpeed = inputMag * moveSpeed;
        currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedVel, accelTime);

        // Dönüş yumuşatma (sadece hareket varken)
        if (inputMag > 0.0001f)
        {
            float targetAngle = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
            float y = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVel, turnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, y, 0f);
        }

        // Fiziksel hareket
        Vector3 delta = moveDir * currentSpeed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + delta);
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
                if (rb2) { rb2.isKinematic = true; rb2.velocity = Vector3.zero; rb2.angularVelocity = Vector3.zero; }
                carried.transform.SetParent(carryPoint, worldPositionStays:false);
                carried.transform.localPosition = Vector3.zero;
                carried.transform.localRotation = Quaternion.identity;

                // İstenirse: Carried layer'ını Player ile çakıştırma (Project Settings > Physics’ten ayarla)
                break;
            }
        }
    }

    public GameObject TakeCarried() => carried;

    public void ClearCarried()
    {
        if (carried == null) return;
        Destroy(carried);
        carried = null;
    }

    void Drop()
    {
        if (carried == null) return;

        var rb2 = carried.GetComponent<Rigidbody>();
        carried.transform.SetParent(null);
        if (rb2)
        {
            rb2.isKinematic = false;
            rb2.velocity = Vector3.zero;
            rb2.angularVelocity = Vector3.zero;
            rb2.AddForce(transform.forward * 2f, ForceMode.VelocityChange);
        }
        carried = null;
    }
}
