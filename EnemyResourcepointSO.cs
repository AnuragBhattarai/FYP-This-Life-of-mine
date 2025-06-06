using UnityEngine;

[CreateAssetMenu(fileName = "EnemyResourcePoint", menuName = "ScriptableObjects/EnemyResourcePointSO", order = 1)]
public class EnemyResourcePointSO : ScriptableObject
{
    public string unitName;
    public GameObject unitPrefab;
    public int resourceValue;
}