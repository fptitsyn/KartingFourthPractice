using UnityEngine;

public class CarSuspension : MonoBehaviour
{
    [Header("Suspension Points")]
    [SerializeField] private Transform fl;
    [SerializeField] private Transform fr;
    [SerializeField] private Transform rl;
    [SerializeField] private Transform rr;

    [Header("Suspension Settings")]
    [SerializeField] private float restLength = 0.4f;
    [SerializeField] private float springTravel = 0.2f;
    [SerializeField] private float springStiffness = 20000f;
    [SerializeField] private float damperStiffness = 3500f;
    [SerializeField] private float wheelRadius = 0.35f;

    [Header("Anti-Roll Bar")]
    [SerializeField] private float frontAntiRollStiffness = 8000f;
    [SerializeField] private float rearAntiRollStiffness = 6000f;

    // Данные для телеметрии
    private float flDistance, frDistance, rlDistance, rrDistance;
    private float flCompression, frCompression, rlCompression, rrCompression;
    private float flSpringForce, frSpringForce, rlSpringForce, rrSpringForce;
    private float flDamperForce, frDamperForce, rlDamperForce, rrDamperForce;

    private Rigidbody rb;
    private LayerMask groundLayer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        groundLayer = LayerMask.GetMask("Default");
    }

    private void FixedUpdate()
    {
        SimulateWheel(fl, ref flCompression, out flDistance, out flSpringForce, out flDamperForce);
        SimulateWheel(fr, ref frCompression, out frDistance, out frSpringForce, out frDamperForce);
        SimulateWheel(rl, ref rlCompression, out rlDistance, out rlSpringForce, out rlDamperForce);
        SimulateWheel(rr, ref rrCompression, out rrDistance, out rrSpringForce, out rrDamperForce);

        ApplyAntiRollBars();
    }

    private void SimulateWheel(Transform pivot, ref float compression,
        out float distance, out float springForce, out float damperForce)
    {
        distance = 0f;
        springForce = 0f;
        damperForce = 0f;

        if (pivot == null) return;

        Vector3 origin = pivot.position;
        Vector3 direction = -pivot.up;
        float maxDist = restLength + springTravel + wheelRadius;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, maxDist, groundLayer))
        {
            distance = hit.distance;

            float currentLength = hit.distance - wheelRadius;
            currentLength = Mathf.Clamp(currentLength,
                restLength - springTravel,
                restLength + springTravel);

            float newCompression = restLength - currentLength;

            springForce = newCompression * springStiffness;

            float compressionVelocity = (newCompression - compression) / Time.fixedDeltaTime;
            damperForce = compressionVelocity * damperStiffness;

            compression = newCompression;

            float totalForce = springForce + damperForce;
            rb.AddForceAtPosition(pivot.up * totalForce, pivot.position, ForceMode.Force);
        }
        else
        {
            compression = 0f;
            distance = maxDist;
        }
    }

    private void ApplyAntiRollBars()
    {
        float frontDiff = flCompression - frCompression;
        float frontForce = frontDiff * frontAntiRollStiffness;

        if (flCompression > -0.0001f)
            rb.AddForceAtPosition(-transform.up * frontForce, fl.position, ForceMode.Force);
        if (frCompression > -0.0001f)
            rb.AddForceAtPosition(transform.up * frontForce, fr.position, ForceMode.Force);

        float rearDiff = rlCompression - rrCompression;
        float rearForce = rearDiff * rearAntiRollStiffness;

        if (rlCompression > -0.0001f)
            rb.AddForceAtPosition(-transform.up * rearForce, rl.position, ForceMode.Force);
        if (rrCompression > -0.0001f)
            rb.AddForceAtPosition(transform.up * rearForce, rr.position, ForceMode.Force);
    }

    private void OnGUI()
    {
        if (!Application.isPlaying) return;

        float screenWidth = Screen.width;
        float x = screenWidth - 370; // Правая сторона с отступом
        float y = 10;
        float lineHeight = 22;

        // === СТИЛИ ===
        GUIStyle headerStyle = new GUIStyle(GUI.skin.label);
        headerStyle.fontStyle = FontStyle.Bold;
        headerStyle.fontSize = 14;
        headerStyle.normal.textColor = new Color(0.2f, 0.8f, 1f); // Голубой

        GUIStyle valueStyle = new GUIStyle(GUI.skin.label);
        valueStyle.fontSize = 12;
        valueStyle.normal.textColor = Color.white;

        GUIStyle wheelStyle = new GUIStyle(GUI.skin.label);
        wheelStyle.fontSize = 11;

        // === ФОН И ЗАГОЛОВОК ===
        GUI.Box(new Rect(x - 10, y - 10, 360, 380), "", GUI.skin.box);

        GUI.Label(new Rect(x, y, 350, lineHeight), "🔩 СИСТЕМА ПОДВЕСКИ", headerStyle);
        y += lineHeight + 5;

        // Разделитель
        GUI.Box(new Rect(x, y, 340, 1), "");
        y += 10;

        // === 1. АЭРОДИНАМИКА ===
        float speed = rb.linearVelocity.magnitude;
        float dragForce = 0.5f * 1.225f * 0.9f * 0.6f * speed * speed;
        float downforce = speed * 70f;

        GUI.Label(new Rect(x, y, 340, lineHeight), "✈️ АЭРОДИНАМИКА", headerStyle);
        y += lineHeight;

        // Сопротивление
        Color dragColor = dragForce < 500 ? Color.white :
                         dragForce < 1000 ? Color.yellow : Color.red;
        valueStyle.normal.textColor = dragColor;
        GUI.Label(new Rect(x + 20, y, 320, lineHeight), $"Сопротивление: {dragForce:F0} Н", valueStyle);
        y += lineHeight;

        // Прижим
        Color downforceColor = downforce < 1000 ? Color.white :
                              downforce < 2000 ? Color.green : Color.cyan;
        valueStyle.normal.textColor = downforceColor;
        GUI.Label(new Rect(x + 20, y, 320, lineHeight), $"Прижим крыла: {downforce:F0} Н", valueStyle);
        y += lineHeight + 10;

        // === 2. СИЛЫ ПОДВЕСКИ ===
        GUI.Label(new Rect(x, y, 340, lineHeight), "🛠️ СИЛЫ ПОДВЕСКИ (Н)", headerStyle);
        y += lineHeight;

        float flTotal = flSpringForce + flDamperForce;
        float frTotal = frSpringForce + frDamperForce;
        float rlTotal = rlSpringForce + rlDamperForce;
        float rrTotal = rrSpringForce + rrDamperForce;

        // Цветовая градация по силе
        wheelStyle.normal.textColor = GetForceColor(flTotal);
        GUI.Label(new Rect(x + 20, y, 160, lineHeight), $"FL: {flTotal:F0}", wheelStyle);

        wheelStyle.normal.textColor = GetForceColor(frTotal);
        GUI.Label(new Rect(x + 180, y, 160, lineHeight), $"FR: {frTotal:F0}", wheelStyle);
        y += lineHeight;

        wheelStyle.normal.textColor = GetForceColor(rlTotal);
        GUI.Label(new Rect(x + 20, y, 160, lineHeight), $"RL: {rlTotal:F0}", wheelStyle);

        wheelStyle.normal.textColor = GetForceColor(rrTotal);
        GUI.Label(new Rect(x + 180, y, 160, lineHeight), $"RR: {rrTotal:F0}", wheelStyle);
        y += lineHeight + 10;

        // === 3. ВЫСОТА КОЛЕС ===
        GUI.Label(new Rect(x, y, 340, lineHeight), "📏 ВЫСОТА КОЛЕС (м)", headerStyle);
        y += lineHeight;

        wheelStyle.normal.textColor = GetHeightColor(flDistance);
        GUI.Label(new Rect(x + 20, y, 160, lineHeight), $"FL: {flDistance:F3}", wheelStyle);

        wheelStyle.normal.textColor = GetHeightColor(frDistance);
        GUI.Label(new Rect(x + 180, y, 160, lineHeight), $"FR: {frDistance:F3}", wheelStyle);
        y += lineHeight;

        wheelStyle.normal.textColor = GetHeightColor(rlDistance);
        GUI.Label(new Rect(x + 20, y, 160, lineHeight), $"RL: {rlDistance:F3}", wheelStyle);

        wheelStyle.normal.textColor = GetHeightColor(rrDistance);
        GUI.Label(new Rect(x + 180, y, 160, lineHeight), $"RR: {rrDistance:F3}", wheelStyle);
        y += lineHeight + 10;

        // === 4. СЖАТИЕ ПОДВЕСКИ ===
        GUI.Label(new Rect(x, y, 340, lineHeight), "📐 СЖАТИЕ ПОДВЕСКИ", headerStyle);
        y += lineHeight;

        wheelStyle.normal.textColor = GetCompressionColor(flCompression);
        GUI.Label(new Rect(x + 20, y, 160, lineHeight), $"FL: {flCompression:F3}", wheelStyle);

        wheelStyle.normal.textColor = GetCompressionColor(frCompression);
        GUI.Label(new Rect(x + 180, y, 160, lineHeight), $"FR: {frCompression:F3}", wheelStyle);
        y += lineHeight;

        wheelStyle.normal.textColor = GetCompressionColor(rlCompression);
        GUI.Label(new Rect(x + 20, y, 160, lineHeight), $"RL: {rlCompression:F3}", wheelStyle);

        wheelStyle.normal.textColor = GetCompressionColor(rrCompression);
        GUI.Label(new Rect(x + 180, y, 160, lineHeight), $"RR: {rrCompression:F3}", wheelStyle);
        y += lineHeight + 10;

        // === 5. ВЫСОТА ЦЕНТРА МАСС ===
        float comHeight = rb.worldCenterOfMass.y;
        Color comColor = comHeight > 0.5f ? Color.yellow :
                        comHeight > 0.3f ? Color.white : Color.green;
        valueStyle.normal.textColor = comColor;
        GUI.Label(new Rect(x, y, 340, lineHeight), $"⚖️ Высота ЦМ: {comHeight:F3} м", valueStyle);
    }

    // === ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ДЛЯ ЦВЕТОВОЙ ИНДИКАЦИИ ===

    private Color GetForceColor(float force)
    {
        float absForce = Mathf.Abs(force);
        if (absForce < 1000) return Color.white;
        if (absForce < 2000) return Color.green;
        if (absForce < 3000) return Color.yellow;
        if (absForce < 4000) return new Color(1f, 0.5f, 0f); // Оранжевый
        return Color.red;
    }

    private Color GetHeightColor(float height)
    {
        if (height < 0.1f) return Color.red;
        if (height < 0.2f) return Color.yellow;
        if (height < 0.3f) return Color.green;
        if (height < 0.4f) return Color.cyan;
        return new Color(0.5f, 0.5f, 1f); // Сиреневый
    }

    private Color GetCompressionColor(float compression)
    {
        if (compression > 0.15f) return Color.red;
        if (compression > 0.1f) return Color.yellow;
        if (compression > 0.05f) return Color.white;
        if (compression > -0.05f) return Color.green;
        if (compression > -0.1f) return Color.cyan;
        return new Color(0.5f, 0.5f, 1f); // Сиреневый (отбой)
    }
}