using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class KartController : MonoBehaviour
{

    [Header("Configuration")]
    [SerializeField] private KartConfig _kartConfig;

    [Header("Physics")]
    [SerializeField] private float _gravity = 9.81f;

    [Header("Engine & drivetrain")]
    [SerializeField] private KartEngine _engine;
    [SerializeField] private float _drivetrainEfficiency = 0.9f;

    [Header("Wheel attachment points")]
    [SerializeField] private Transform _frontLeftWheel;
    [SerializeField] private Transform _frontRightWheel;
    [SerializeField] private Transform _rearLeftWheel;
    [SerializeField] private Transform _rearRightWheel;

    private Quaternion _frontLeftInitialLocalRot;
    private Quaternion _frontRightInitialLocalRot;

    [Header("Input (New Input System)")]
    [SerializeField] private InputActionReference _moveActionRef;

    private float _throttleInput;
    private float _steerInput; 

    [Header("Handbrake")]
    [SerializeField] private InputActionReference _handbrakeActionRef;
    [SerializeField] private float _handbrakeDragMultiplier = 3f;
    [SerializeField] private bool _handbrakeEnabled = true;

    // Для сбора данных телеметрии
    private float _totalRearLongitudinalForce;
    private float _totalFrontLateralForce;
    private float _frontLeftVLat, _frontRightVLat, _rearLeftVLat, _rearRightVLat;
    private Vector3 _lastVelocity;
    private float _acceleration;

    private bool _isHandbrakePressed;
    private float _originalLateralStiffness; 

    private Rigidbody _rb;

    private float _frontLeftNormalForce;
    private float _frontRightNormalForce;
    private float _rearLeftNormalForce;
    private float _rearRightNormalForce;


    private void Start()
    {
        _rb = GetComponent<Rigidbody>();
        ComputeStaticWheelLoads();
        Initialize();
        _originalLateralStiffness = _kartConfig.frontLateralStiffness;
    }

    private void Update()
    {
        ReadInput();    
        RotateFrontWheels(); // Поворачиваем колеса
    }

    private void FixedUpdate()
    {
        // Считаем ускорение
        Vector3 velocityChange = _rb.linearVelocity - _lastVelocity;
        _acceleration = velocityChange.magnitude / Time.fixedDeltaTime;
        _lastVelocity = _rb.linearVelocity;

        _totalRearLongitudinalForce = 0f;
        _totalFrontLateralForce = 0f;

        ApplyWheelForces(_frontLeftWheel, _frontLeftNormalForce, isDriven: false);
        ApplyWheelForces(_frontRightWheel, _frontRightNormalForce, isDriven: false);

        ApplyWheelForces(_rearLeftWheel, _rearLeftNormalForce, isDriven: true);
        ApplyWheelForces(_rearRightWheel, _rearRightNormalForce, isDriven: true);
    }

    private void ComputeStaticWheelLoads()
    {
        // 1. Получаем массу из Rigidbody
        float mass = _rb.mass;

        // 2. Рассчитываем общий вес
        float totalWeight = mass * _gravity;

        // 3. Распределяем вес по осям
        float frontWeight = totalWeight * _kartConfig.frontAxleShare;
        float rearWeight = totalWeight * (1f - _kartConfig.frontAxleShare);

        // 4. Делим поровну между левым и правым колесом на каждой оси
        _frontLeftNormalForce = frontWeight * 0.5f;
        _frontRightNormalForce = frontWeight * 0.5f;

        _rearLeftNormalForce = rearWeight * 0.5f;
        _rearRightNormalForce = rearWeight * 0.5f;
    }

    private void Initialize()
    {

        if (_frontLeftWheel != null)
            _frontLeftInitialLocalRot = _frontLeftWheel.localRotation;
        if (_frontRightWheel != null)
            _frontRightInitialLocalRot = _frontRightWheel.localRotation;
    }

    private void ReadInput()
    {
        Vector2 move = _moveActionRef.action.ReadValue<Vector2>();
        _steerInput = Mathf.Clamp(move.x, -1f, 1f);
        _throttleInput = Mathf.Clamp(move.y, -1f, 1f);

        _isHandbrakePressed = _handbrakeActionRef.action.ReadValue<float>() > 0.5f;
 
    }

    private void RotateFrontWheels()
    {
        float steerAngle = _kartConfig.maxSteerAngle * _steerInput;
        Quaternion steerRotation = Quaternion.Euler(0f, steerAngle, 0f);

        if (_frontLeftWheel != null)
            _frontLeftWheel.localRotation = _frontLeftInitialLocalRot * steerRotation;
        if (_frontRightWheel != null)
            _frontRightWheel.localRotation = _frontRightInitialLocalRot * steerRotation;
    }

    private void OnEnable()
    {
        if (_moveActionRef != null && _moveActionRef.action != null)
            _moveActionRef.action.Enable();

        // Включаем ручной тормоз 
        if (_handbrakeActionRef != null && _handbrakeActionRef.action != null)
            _handbrakeActionRef.action.Enable();
    }

    private void OnDisable()
    {
        if (_moveActionRef != null && _moveActionRef.action != null)
            _moveActionRef.action.Disable();

        // Выключаем ручной тормоз 
        if (_handbrakeActionRef != null && _handbrakeActionRef.action != null)
            _handbrakeActionRef.action.Disable();
    }

    private void ApplyWheelForces(Transform wheel, float normalForce, bool isDriven)
    {

        if (wheel == null || _rb == null) return;

        Vector3 wheelPos = wheel.position;
        Vector3 wheelForward = wheel.forward;
        Vector3 wheelRight = wheel.right;
        Vector3 v = _rb.GetPointVelocity(wheelPos);

        float vLong = Vector3.Dot(v, wheelForward);
        float vLat = Vector3.Dot(v, wheelRight);

        float Fx = 0f;
        float Fy = 0f;

        // Записываем боковое скольжение для телеметрии
        if (wheel == _frontLeftWheel) _frontLeftVLat = vLat;
        if (wheel == _frontRightWheel) _frontRightVLat = vLat;
        if (wheel == _rearLeftWheel) _rearLeftVLat = vLat;
        if (wheel == _rearRightWheel) _rearRightVLat = vLat;

        // 1) продольная сила от двигателя
        if (isDriven)
        {
            Vector3 bodyForward = transform.forward;
            float speedAlongForward = Vector3.Dot(_rb.linearVelocity, bodyForward);

            // Получаем момент от двигателя
            float engineTorque = _engine.Simulate(
                _throttleInput,
                speedAlongForward,
                Time.fixedDeltaTime
            );

            float totalWheelTorque = engineTorque * _kartConfig.gearRatio * _drivetrainEfficiency;
            float wheelTorque = totalWheelTorque * 0.5f; 

            if (!(_isHandbrakePressed && isDriven && _handbrakeEnabled))
            {
                Fx += wheelTorque / _kartConfig.wheelRadius;
            }
        }

        // 2) сопротивление качению
        float currentRollingResistance = _kartConfig.rollingResistance;

        // Ручной тормоз: увеличиваем сопротивление для задних колес
        if (_isHandbrakePressed && isDriven && _handbrakeEnabled)
        {
            currentRollingResistance *= _handbrakeDragMultiplier;

            // Сила, направленная против движения колеса
            float brakeForce = -Mathf.Sign(vLong) * normalForce * _kartConfig.frictionCoefficient * 0.8f;
            Fx += brakeForce;
        }

        Fx += -currentRollingResistance * vLong;

        // 3) боковая сила
        float currentLateralStiffness = _kartConfig.frontLateralStiffness;

        // Ручной тормоз: убираем боковое сцепление у задних колес
        if (_isHandbrakePressed && isDriven && _handbrakeEnabled)
        {
            currentLateralStiffness = 0f; // Полное отсутствие бокового сцепления
        }

        Fy += -currentLateralStiffness * vLat;

        // 4) фрикционный круг 
        // При ручном тормозе ослабляем фрикционный круг для задних колес
        float currentFrictionCoefficient = _kartConfig.frictionCoefficient;
        if (_isHandbrakePressed && isDriven && _handbrakeEnabled)
        {
            currentFrictionCoefficient *= 0.3f; 
        }

        float frictionLimit = currentFrictionCoefficient * normalForce;
        float forceLength = Mathf.Sqrt(Fx * Fx + Fy * Fy);

        if (forceLength > frictionLimit && forceLength > 1e-6f)
        {
            float scale = frictionLimit / forceLength;
            Fx *= scale;
            Fy *= scale;
        }

        // Собираем силы для телеметрии
        if (isDriven) 
        {
            _totalRearLongitudinalForce += Fx;
        }
        else 
        {
            _totalFrontLateralForce += Mathf.Abs(Fy);
        }

        // 5) мировая сила
        Vector3 force = wheelForward * Fx + wheelRight * Fy;

        _rb.AddForceAtPosition(force, wheelPos, ForceMode.Force);
    }

    private void OnGUI()
    {
        float speedMs = _rb.linearVelocity.magnitude;
        float speedKmh = speedMs * 3.6f;

        float x = 10;
        float y = 10;
        float lineHeight = 22;

        // === СТИЛИ ===
        GUIStyle headerStyle = new GUIStyle(GUI.skin.label);
        headerStyle.fontStyle = FontStyle.Bold;
        headerStyle.fontSize = 14;
        headerStyle.normal.textColor = Color.yellow;

        GUIStyle valueStyle = new GUIStyle(GUI.skin.label);
        valueStyle.fontSize = 12;
        valueStyle.normal.textColor = Color.white;

        GUIStyle warningStyle = new GUIStyle(GUI.skin.label);
        warningStyle.fontStyle = FontStyle.Bold;
        warningStyle.fontSize = 13;
        warningStyle.normal.textColor = new Color(1f, 0.5f, 0f); // Оранжевый

        GUIStyle criticalStyle = new GUIStyle(GUI.skin.label);
        criticalStyle.fontStyle = FontStyle.Bold;
        criticalStyle.fontSize = 13;
        criticalStyle.normal.textColor = Color.red;

        // === ФОН ===
        GUI.Box(new Rect(x - 5, y - 5, 340, 250), "", GUI.skin.box);

        // === ЗАГОЛОВОК ===
        GUI.Label(new Rect(x, y, 300, lineHeight), "🏎️ ТЕЛЕМЕТРИЯ КАРТА", headerStyle);
        y += lineHeight + 5;

        // Разделитель
        GUI.Box(new Rect(x, y, 320, 1), "");
        y += 10;

        // 1. Скорость (с цветовой индикацией)
        Color speedColor = speedKmh < 30 ? Color.white :
                          speedKmh < 60 ? Color.green :
                          speedKmh < 90 ? Color.yellow : Color.red;
        valueStyle.normal.textColor = speedColor;
        GUI.Label(new Rect(x, y, 300, lineHeight), $"🚀 Скорость: {speedKmh:F1} км/ч ({speedMs:F1} м/с)", valueStyle);
        y += lineHeight;

        // 2. RPM (с цветовой индикацией)
        Color rpmColor = _engine.CurrentRpm < 3000 ? Color.white :
                        _engine.CurrentRpm < 5000 ? Color.green :
                        _engine.CurrentRpm < 7000 ? Color.yellow : Color.red;
        valueStyle.normal.textColor = rpmColor;
        GUI.Label(new Rect(x, y, 300, lineHeight), $"⚙️ RPM: {_engine.CurrentRpm:F0} об/мин", valueStyle);
        y += lineHeight;

        // 3. Момент двигателя
        valueStyle.normal.textColor = Color.cyan;
        GUI.Label(new Rect(x, y, 300, lineHeight), $"🔧 Момент: {_engine.CurrentTorque:F0} Н·м", valueStyle);
        y += lineHeight;

        // 4. Ускорение (с цветовой индикацией)
        Color accelColor = Mathf.Abs(_acceleration) < 5 ? Color.white :
                          Mathf.Abs(_acceleration) < 10 ? Color.yellow : Color.red;
        valueStyle.normal.textColor = accelColor;
        string accelIcon = _acceleration > 0 ? "📈" : _acceleration < 0 ? "📉" : "➡️";
        GUI.Label(new Rect(x, y, 300, lineHeight), $"{accelIcon} Ускорение: {_acceleration:F1} м/с²", valueStyle);
        y += lineHeight;

        // 5. Продольная сила задней оси
        valueStyle.normal.textColor = new Color(0.8f, 0.4f, 1f); // Фиолетовый
        GUI.Label(new Rect(x, y, 300, lineHeight), $"🔽 Fx задняя ось: {_totalRearLongitudinalForce:F0} Н", valueStyle);
        y += lineHeight;

        // 6. Боковая сила передней оси
        valueStyle.normal.textColor = new Color(1f, 0.6f, 0f); // Оранжевый
        GUI.Label(new Rect(x, y, 300, lineHeight), $"🔄 Fy передняя ось: {_totalFrontLateralForce:F0} Н", valueStyle);
        y += lineHeight;

        // Разделитель
        y += 5;
        GUI.Box(new Rect(x, y, 320, 1), "");
        y += 10;

        // 7-8. Боковое скольжение колес
        GUI.Label(new Rect(x, y, 300, lineHeight), "🛞 Скольжение колес:", headerStyle);
        y += lineHeight;

        valueStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(x + 20, y, 300, lineHeight),
            $"Перед: L={_frontLeftVLat:F2} | R={_frontRightVLat:F2}", valueStyle);
        y += lineHeight;

        GUI.Label(new Rect(x + 20, y, 300, lineHeight),
            $"Зад:  L={_rearLeftVLat:F2} | R={_rearRightVLat:F2}", valueStyle);
        y += lineHeight + 5;

        // 9. Ручной тормоз
        if (_isHandbrakePressed)
        {
            GUI.Box(new Rect(x - 5, y - 5, 330, 40), "", GUI.skin.box);
            warningStyle.normal.textColor = Color.red;
            GUI.Label(new Rect(x, y, 300, lineHeight), "⚠️ РУЧНОЙ ТОРМОЗ: АКТИВЕН", warningStyle);
            y += lineHeight;
            warningStyle.normal.textColor = new Color(1f, 0.7f, 0f);
            GUI.Label(new Rect(x, y, 300, lineHeight), "Боковое сцепление снижено", warningStyle);
        }
        else
        {
            valueStyle.normal.textColor = Color.gray;
            GUI.Label(new Rect(x, y, 300, lineHeight), "✅ Ручной тормоз: неактивен", valueStyle);
        }
    }
}