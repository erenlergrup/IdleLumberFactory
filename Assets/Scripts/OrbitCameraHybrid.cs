using UnityEngine;

public class OrbitCameraHybrid : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Orbit")]
    public float distance = 4.5f;       // biraz daha geriden
    public float height = 1.4f;         // biraz daha alttan
    public float minPitch = 12f;
    public float maxPitch = 55f;
    [Tooltip("Default pitch reset değeri")]
    public float defaultPitch = 22f;

    [Header("Mouse Orbit (RMB ile)")]
    [Tooltip("Sağ tık basılıyken serbest bakış")]
    public bool holdRightToOrbit = true;
    public float rmbYawSensitivity = 150f;     // sağ tıkla orbit hızı
    public float rmbPitchSensitivity = 100f;
    public bool invertY = false;

    [Header("Auto Align")]
    public bool autoAlignWhenMoving = true;    // yürürken yavaşça arkaya hizala
    public float alignSpeed = 2.5f;            // hizalama sertliği
    [Tooltip("Sadece W (ileri) basılıyken hizala")]
    public bool alignOnlyWhenForward = true;

    [Header("Smoothing")]
    public float posSmoothTime = 0.08f;        // konum yumuşatma
    public float rotSmooth = 12f;              // bakış yumuşatma (büyük = daha sert)

    [Header("Collision")]
    public LayerMask clipMask = ~0;
    public float clipRadius = 0.2f;

    [Header("Zoom")]
    public float zoomSpeed = 5f;
    public float minDistance = 2.5f;
    public float maxDistance = 7.5f;

    [Header("Look Ahead")]
    [Tooltip("Hareket ederken ileriye bakış ofseti (0=kapalı)")]
    public float lookAhead = 0.6f;             // küçük bir ileriye bakış

    float yaw, pitch;
    Vector3 posVel;
    float defaultDistance;

    [HideInInspector] public Vector3 lastMoveDir = Vector3.zero;
    [HideInInspector] public bool isMovingForward = false;

    void Start()
    {
        if (!target) { enabled = false; return; }

        // ilk hizalama
        yaw = target.eulerAngles.y;
        pitch = Mathf.Clamp(defaultPitch, minPitch, maxPitch);
        defaultDistance = distance;

        // imleç serbest; RMB basınca orbit
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void LateUpdate()
    {
        if (!target) return;

        // --- 1) Mouse delta (sadece RMB basılıysa) ---
        bool orbiting = !holdRightToOrbit || Input.GetMouseButton(1);
        if (orbiting)
        {
            float mx = Input.GetAxis("Mouse X");
            float my = Input.GetAxis("Mouse Y");
            yaw   += mx * rmbYawSensitivity   * Time.deltaTime;
            pitch += (invertY ? 1f : -1f) * my * rmbPitchSensitivity * Time.deltaTime;
        }

        // --- 2) Auto-align (opsiyonel) ---
        if (autoAlignWhenMoving && lastMoveDir.sqrMagnitude > 0.01f)
        {
            if (!alignOnlyWhenForward || isMovingForward)
            {
                float targetYaw = Mathf.Atan2(lastMoveDir.x, lastMoveDir.z) * Mathf.Rad2Deg;
                yaw = Mathf.LerpAngle(yaw, targetYaw, 1f - Mathf.Exp(-alignSpeed * Time.deltaTime));
            }
        }

        // --- 3) Pitch sınırı + Zoom ---
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.001f)
            distance = Mathf.Clamp(distance - scroll * zoomSpeed * Time.deltaTime, minDistance, maxDistance);

        // --- 4) İstenen pozisyon ---
        Quaternion orbitRot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desiredPos = target.position
                           - (orbitRot * Vector3.forward) * distance
                           + Vector3.up * height;

        // --- 5) Çarpışma ---
        Vector3 from = target.position + Vector3.up * height;
        Vector3 dir = desiredPos - from;
        if (Physics.SphereCast(from, clipRadius, dir.normalized, out RaycastHit hit,
            dir.magnitude, clipMask, QueryTriggerInteraction.Ignore))
        {
            desiredPos = hit.point + hit.normal * clipRadius;
        }

        // --- 6) Yumuşak takip ---
        transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref posVel, posSmoothTime);

        // --- 7) Bakış (hafif ileriye) ---
        Vector3 focus = target.position + Vector3.up * (height * 0.6f);
        if (lookAhead > 0f && lastMoveDir.sqrMagnitude > 0.01f)
            focus += lastMoveDir.normalized * lookAhead;

        Quaternion look = Quaternion.LookRotation(focus - transform.position, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-rotSmooth * Time.deltaTime));

        // --- 8) RESET (C) -> yaw/pitch/zoom tam sıfırla ---
        if (Input.GetKeyDown(KeyCode.C))
        {
            yaw = target.eulerAngles.y;
            pitch = Mathf.Clamp(defaultPitch, minPitch, maxPitch);
            distance = defaultDistance;

            // anında snap (istersen), sonra tekrar yumuşar
            Quaternion snapRot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 snapPos = target.position
                            - (snapRot * Vector3.forward) * distance
                            + Vector3.up * height;
            transform.position = snapPos;
            transform.rotation = Quaternion.LookRotation(
                (target.position + Vector3.up * (height * 0.6f)) - transform.position, Vector3.up);
            posVel = Vector3.zero;
        }
    }

    // PlayerController çağırır
    public void ReportMoveDir(Vector3 moveDirOnPlane, bool movingForward)
    {
        lastMoveDir = moveDirOnPlane;
        isMovingForward = movingForward;
    }
}
