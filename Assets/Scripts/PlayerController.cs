using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;            // maksimum yürüyüş hızı
    public float accelTime = 0.12f;         // hızlanma-yavaşlama yumuşatma süresi
    public float turnSmoothTime = 0.08f;    // yön dönme yumuşatma
    public bool adIsDiagonalForward = true; // A ya da D tek başına basılırsa ileri-yan git

    [Header("Interact")]
    public Transform carryPoint;
    public float hitRange = 2f;
    public LayerMask treeMask;

    // internals
    Rigidbody rb;
    Camera cam;

    Vector3 inputDirXZ;   // ham (x,z) giriş
    Vector3 velRef;       // SmoothDamp için hız ref
    float turnVelRef;     // SmoothDampAngle için
    GameObject carried;

    OrbitCameraHybrid camHybrid; // Kameraya yön raporu için (ReportMoveDir)

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true; // X/Z dönmeleri kilitle
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        cam = Camera.main;
        camHybrid = cam ? cam.GetComponent<OrbitCameraHybrid>() : null;
    }

    void Update()
    {
        // --- 1) Ham input oku (WASD) ---
        float h = Input.GetAxisRaw("Horizontal"); // A(-1)  D(+1)
        float v = Input.GetAxisRaw("Vertical");   // S(-1)  W(+1)

        // İstenirse A/D tek başına "ileri-yan" gibi davransın (strafe değil)
        if (adIsDiagonalForward)
        {
            if (Mathf.Approximately(v, 0f) && !Mathf.Approximately(h, 0f))
            {
                // A: (h<0) => W+A, D: (h>0) => W+D
                v = 1f;
            }
        }

        inputDirXZ = new Vector3(h, 0f, v);
        if (inputDirXZ.sqrMagnitude > 1f) inputDirXZ.Normalize();

        // --- 2) Etkileşimler ---
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 pos = transform.position + transform.forward * 1.0f;
            var cols = Physics.OverlapSphere(pos, hitRange, treeMask);
            foreach (var c in cols)
            {
                var t = c.GetComponentInParent<Tree>();
                if (t != null) { t.Hit(1); break; }
            }
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (carried == null) TryPickup();
            else Drop();
        }

        // --- 3) Kameraya hareket yönünü bildir (auto-align için) ---
        if (camHybrid && cam)
        {
            Vector3 camF = cam.transform.forward; camF.y = 0f; camF.Normalize();
            Vector3 camR = cam.transform.right;   camR.y = 0f; camR.Normalize();
            Vector3 moveDir = (camF * v + camR * h);
            if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

            // "ileri" sayalım mı? Kamera ileri eksenine projeksiyon ile karar verelim
            bool movingForward = Vector3.Dot(moveDir, camF) > 0.25f; // biraz tolerans
            camHybrid.ReportMoveDir(moveDir, movingForward);
        }
    }

    void FixedUpdate()
    {
        // --- 4) Kamera-bazlı hedef hız ---
        Vector3 desiredVel = GetDesiredVelocity();   // XZ hedef hız
        Vector3 curVel = rb.linearVelocity;

        // XZ SmoothDamp, Y yerçekimine bırak
        Vector3 newVel = new Vector3(
            Mathf.SmoothDamp(curVel.x, desiredVel.x, ref velRef.x, accelTime),
            curVel.y,
            Mathf.SmoothDamp(curVel.z, desiredVel.z, ref velRef.z, accelTime)
        );

        rb.linearVelocity = newVel;

        // --- 5) Dönüşü hareket yönüne yumuşak hizala ---
        Vector3 flatVel = new Vector3(newVel.x, 0f, newVel.z);
        if (flatVel.sqrMagnitude > 0.0001f)
        {
            float targetAngle = Mathf.Atan2(flatVel.x, flatVel.z) * Mathf.Rad2Deg;
            float y = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnVelRef, turnSmoothTime);
            rb.MoveRotation(Quaternion.Euler(0f, y, 0f));
        }

        // çarpışmadan gelen açısal momenti söndür
        rb.angularVelocity = Vector3.zero;
    }

    Vector3 GetDesiredVelocity()
    {
        if (!cam) return inputDirXZ * moveSpeed;

        // Kamera düzleminde hareket
        float h = inputDirXZ.x;
        float v = inputDirXZ.z;

        Vector3 camF = cam.transform.forward; camF.y = 0f; camF.Normalize();
        Vector3 camR = cam.transform.right;   camR.y = 0f; camR.Normalize();

        Vector3 dir = camF * v + camR * h;
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        return dir * moveSpeed;
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

                int ignoreLayer = LayerMask.NameToLayer("IgnorePlayer");
                if (ignoreLayer >= 0) carried.layer = ignoreLayer;

                carried.transform.SetParent(carryPoint);
                carried.transform.localPosition = Vector3.zero;
                carried.transform.localRotation = Quaternion.identity;
                break;
            }
        }
    }

    void Drop()
    {
        if (!carried) return;

        var rb2 = carried.GetComponent<Rigidbody>();
        carried.transform.SetParent(null);

        if (rb2)
        {
            int logLayer = LayerMask.NameToLayer("Log");
            if (logLayer >= 0) carried.layer = logLayer;

            rb2.isKinematic = false;
            rb2.AddForce(transform.forward * 2f, ForceMode.VelocityChange);
        }

        carried = null;
    }

    public GameObject TakeCarried() => carried;

    public void ClearCarried()
    {
        if (!carried) return;
        Destroy(carried);
        carried = null;
    }
}
