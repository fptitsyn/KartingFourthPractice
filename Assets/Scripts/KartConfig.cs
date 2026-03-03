using UnityEngine;

[CreateAssetMenu(fileName = "KartConfig", menuName = "Karting/Kart Config")]
public class KartConfig : ScriptableObject
{
    [Header("Chassis")]
    public float mass = 80f;

    [Header("Wheel Physics")]
    public float frictionCoefficient = 4.0f;
    public float frontLateralStiffness = 1000f;
    public float rearLateralStiffness = 80f;
    public float rollingResistance = 0.5f;

    [Header("Steering")]
    public float maxSteerAngle = 30f;

    [Header("Engine")]
    public AnimationCurve engineTorqueCurve;
    public float engineInertia = 0.2f;
    public float maxRpm = 8000f;

    [Header("Drivetrain")]
    public float gearRatio = 8f;
    public float wheelRadius = 0.3f;

    [Header("Weight Distribution")]
    [Range(0f, 1f)]
    public float frontAxleShare = 0.4f;

    // Метод для создания кривой по умолчанию
    private void OnEnable()
    {
        if (engineTorqueCurve == null || engineTorqueCurve.keys.Length == 0)
        {
            CreateDefaultTorqueCurve();
        }
    }

    private void CreateDefaultTorqueCurve()
    {
        engineTorqueCurve = new AnimationCurve();
        engineTorqueCurve.AddKey(1000f, 200f);
        engineTorqueCurve.AddKey(3000f, 380f);
        engineTorqueCurve.AddKey(6000f, 350f);
        engineTorqueCurve.AddKey(8000f, 250f);
    }
}