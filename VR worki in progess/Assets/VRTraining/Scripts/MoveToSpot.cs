using System.Collections;
using UnityEngine;

/// <summary>Glides this object (e.g. the instruction panel) to a spot. Call MoveTo from a Unity Event.</summary>
public class MoveToSpot : MonoBehaviour
{
    public float moveSeconds = 0.6f;

    public void MoveTo(Transform spot)
    {
        if (!spot) return;
        StopAllCoroutines();
        StartCoroutine(Glide(spot));
    }

    IEnumerator Glide(Transform spot)
    {
        Vector3 p0 = transform.position;
        Quaternion r0 = transform.rotation;
        for (float t = 0; t < 1; t += Time.deltaTime / moveSeconds)
        {
            transform.SetPositionAndRotation(Vector3.Lerp(p0, spot.position, t), Quaternion.Slerp(r0, spot.rotation, t));
            yield return null;
        }
        transform.SetPositionAndRotation(spot.position, spot.rotation);
    }
}
