using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;                      // yürüyüş hızı (dünya hızı)
    [Range(0.01f, 0.4f)] public float accelTime = 0.12f;
    [Range(0.01f, 0.3f)] public float turnSmoothTime = 0.08f;
    public bool adIsDiagonalForward = true;           // A/D tek başına -> W+A / W+D gibi ileri-yan

    [Header("Interact")]
    public Transform carryPoint;
    public float hitRange = 2f;
    public LayerMask treeMask;

    [Header("Animation")]
    public Animator animator;                         // karakter üstündeki Animator
    public string speedParam = "Speed";               // Blend Tree param (0=Idle, 1=Walk)
    public string walkMulParam = "WalkMul";           // (ops.) state Speed Multiplier param
    public float baseAnimatorSpeed = 1.15f;           // tüm animatöre çarpan (Walk yavaşsa 1.1–1.3)
    public Vector2 walkMulRange = new Vector2(1.0f, 1.35f); // yavaş→hızlı için multiplier aralığı

    // internals
    Rigidbody rb;
    Camera cam;

    Vector3 inputDirXZ;     // ham (x,z) giriş
    Vector3 velRef;         // SmoothDamp ref
    float turnVelRef;       // SmoothDampAngle ref
    GameObject carried;

    OrbitCameraHybrid camHybrid;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true; // X/Z dönmeyi kilitle, Y serbest
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        cam = Camera.main;
        camHybrid = cam ? cam.GetComponent<OrbitCameraHybrid>() : null;

        if (!animator) animator = GetComponentInChildren<Animator>();
        if (animator) animator.applyRootMotion = false;      // hareketi kod sürüyor
        if (animator) animator.speed = baseAnimatorSpeed;    // global hız çarpanı
    }

    void Update()
    {
        // --- 1) Girdi
        float h = Input.GetAxisRaw("Horizontal"); // A(-1)  D(+1)
        float v = Input.GetAxisRaw("Vertical");   // S(-1)  W(+1)

        if (adIsDiagonalForward && Mathf.Approximately(v, 0f) && !Mathf.Approximately(h, 0f))
            v = 1f; // A/D tek basılıysa çapraz ileri varsay

        inputDirXZ = new Vector3(h, 0f, v);
        if (inputDirXZ.sqrMagnitude > 1f) inputDirXZ.Normalize();

        // --- 2) Etkileşim
        if (Input.GetMouseButtonDown(0))
        {
            var pos = transform.position + transform.forward * 1.0f;
            foreach (var c in Physics.OverlapSphere(pos, hitRange, treeMask))
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

        // --- 3) Kameraya yön bildir (kamera auto-align için)
        if (camHybrid && cam)
        {
            Vector3 camF = cam.transform.forward; camF.y = 0f; camF.Normalize();
            Vector3 camR = cam.transform.right;   camR.y = 0f; camR.Normalize();
            Vector3 moveDir = (camF * v + camR * h);
            if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

            bool movingForward = Vector3.Dot(moveDir, camF) > 0.25f;
            camHybrid.ReportMoveDir(moveDir, movingForward);
        }
    }

    void FixedUpdate()
    {
        // --- 4) Hedef hız (kamera düzleminde)
        Vector3 desiredVel = GetDesiredVelocity();
        Vector3 curVel = rb.linearVelocity;

        // XZ'yi SmoothDamp, Y'yi aynen bırak (yerçekimi)
        Vector3 newVel = new Vector3(
            Mathf.SmoothDamp(curVel.x, desiredVel.x, ref velRef.x, accelTime),
            curVel.y,
            Mathf.SmoothDamp(curVel.z, desiredVel.z, ref velRef.z, accelTime)
        );
        rb.linearVelocity = newVel;

        // --- 5) Dönüşü hareket yönüne yumuşat
        Vector3 flatVel = new Vector3(newVel.x, 0f, newVel.z);
        if (flatVel.sqrMagnitude > 0.0001f)
        {
            float targetAngle = Mathf.Atan2(flatVel.x, flatVel.z) * Mathf.Rad2Deg;
            float y = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnVelRef, turnSmoothTime);
            rb.MoveRotation(Quaternion.Euler(0f, y, 0f));
        }
        rb.angularVelocity = Vector3.zero;

        // --- 6) Animasyon sürüşü
        if (animator)
        {
            // 0..1 hız (dünya hızına göre normalize)
            float speed01 = Mathf.Clamp01(flatVel.magnitude / Mathf.Max(0.01f, moveSpeed));
            animator.SetFloat(speedParam, speed01);

            // Walk state’te “Speed Multiplier” a bağlanacak parametre
            if (!string.IsNullOrEmpty(walkMulParam))
            {
                float mul = Mathf.Lerp(walkMulRange.x, walkMulRange.y, speed01);
                animator.SetFloat(walkMulParam, mul);
            }
        }
    }

    Vector3 GetDesiredVelocity()
    {
        if (!cam) return inputDirXZ * moveSpeed;

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
        foreach (var c in Physics.OverlapSphere(transform.position, 2f))
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
