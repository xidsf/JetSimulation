using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace HomingMissile
{
public class homing_missile_pointer : MonoBehaviour
{
   public GameObject target;
   private void FixedUpdate()
    {
        if (target == null || !target.activeInHierarchy)
        {
            return;
        }

        transform.LookAt(target.transform.position);
   }
}
}
