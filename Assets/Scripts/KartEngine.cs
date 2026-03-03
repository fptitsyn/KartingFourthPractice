using UnityEngine;

/// <summary>
/// KartEngine.
/// Этап 1: простейший двигатель с постоянным моментом.
/// </summary>
public class KartEngine : MonoBehaviour
{

    [SerializeField] private KartConfig _kartConfig;

    [Header("RPM settings")]
    [SerializeField] private float _idleRpm = 1000f;
    [SerializeField] private float _maxRpm = 8000f;
    [SerializeField] private float _revLimiterRpm = 7500f;

    [Header("Torque curve")]
    [Tooltip("X = rpm, Y = максимальный момент при полном газе (Н*м).")]
    [SerializeField] private AnimationCurve _torqueCurve;

    [Header("Inertia & response")]
    [Tooltip("Момент инерции маховика J, кг*м^2.")]
    [SerializeField] private float _flywheelInertia = 0.2f;

    [Tooltip("Скорость отклика газа (1/с).")]
    [SerializeField] private float _throttleResponse = 5f;

    [Header("Losses & load")]
    [Tooltip("Внутренние потери, Н*м/ rpm.")]
    [SerializeField] private float _engineFrictionCoeff = 0.02f;

    [Tooltip("Нагрузка от машины, Н*м / (м/с).")]
    [SerializeField] private float _loadTorqueCoeff = 5f;

    public float CurrentRpm { get; private set; }
    public float CurrentTorque { get; private set; }
    public float SmoothedThrottle { get; private set; }
    public float RevLimiterFactor { get; private set; } = 1f;

    private float _invInertiaFactor;

    private void Start()
    {
        CurrentRpm = _idleRpm;
        _torqueCurve = _kartConfig.engineTorqueCurve;
        _maxRpm = _kartConfig.maxRpm;
        _flywheelInertia = _kartConfig.engineInertia;
        _invInertiaFactor = 60f / (2f * Mathf.PI * Mathf.Max(_flywheelInertia, 0.0001f));
    }

    public float Simulate(float throttleInput, float forwardSpeed, float deltaTime)
    {
        // 1. Сглаживание газа 
        float targetThrottle = Mathf.Clamp(throttleInput, -1f, 1f);
        SmoothedThrottle = Mathf.MoveTowards(SmoothedThrottle, targetThrottle,
                                            _throttleResponse * deltaTime);
        UpdateRevLimiterFactor();

        // 2. Момент двигателя из кривой
        float maxTorqueAtRpm = _torqueCurve.Evaluate(CurrentRpm);
        float effectiveThrottle = SmoothedThrottle * RevLimiterFactor;
        float driveTorque = maxTorqueAtRpm * effectiveThrottle;

        // 3. Потери и нагрузка
        float frictionTorque = _engineFrictionCoeff * CurrentRpm;        // Внутреннее трение
        float loadTorque = _loadTorqueCoeff * Mathf.Abs(forwardSpeed);   // Нагрузка от движения

        // 4. Чистый момент 
        float netTorque = driveTorque - frictionTorque - loadTorque;

        // 5. Расчет изменения RPM 
        float rpmDot = netTorque * _invInertiaFactor;

        // 6. Интегрируем
        CurrentRpm += rpmDot * deltaTime;

        // 7. Ограничения
        if (CurrentRpm < _idleRpm) CurrentRpm = _idleRpm;
        if (CurrentRpm > _maxRpm) CurrentRpm = _maxRpm;

        // 8. Запоминаем текущий момент
        CurrentTorque = driveTorque;

        return CurrentTorque;
    }

    private void UpdateRevLimiterFactor()
    {
        if (CurrentRpm <= _revLimiterRpm)
        {
            RevLimiterFactor = 1f;
            return;
        }

        if (CurrentRpm >= _maxRpm)
        {
            RevLimiterFactor = 0f;
            return;
        }

        float t = (CurrentRpm - _revLimiterRpm) / (_maxRpm - _revLimiterRpm);
        RevLimiterFactor = 1f - t;
    }
}