using System;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    public int health;
    internal void ReceiveDamage(int damageToInflict){
        health -= damageToInflict;
    }
}
