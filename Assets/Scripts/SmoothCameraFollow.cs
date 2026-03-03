using UnityEngine;

public class SmoothCameraFollow : MonoBehaviour
{
    [Header("Target Settings")]
    [SerializeField] private Transform target; // Цель (машинка)

    [Header("Camera Settings")]
    [SerializeField] private Vector3 offset = new Vector3(0, 5, -10); // Смещение камеры
    [SerializeField] private float smoothSpeed = 0.125f; // Скорость плавного движения

    [Header("Look Settings")]
    [SerializeField] private bool lookAtTarget = true; // Смотреть на цель
    [SerializeField] private float lookSpeed = 1f; // Скорость поворота

    void LateUpdate()
    {
        // Вычисляем желаемую позицию камеры
        Vector3 desiredPosition = target.position + offset;

        // Плавно перемещаем камеру к желаемой позиции
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;

        // Плавно поворачиваем камеру к цели (если включено)
        if (lookAtTarget)
        {
            Quaternion targetRotation = Quaternion.LookRotation(target.position - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, lookSpeed * Time.deltaTime);
        }
    }

    // Метод для изменения цели во время выполнения
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    // Метод для изменения смещения во время выполнения
    public void SetOffset(Vector3 newOffset)
    {
        offset = newOffset;
    }
}