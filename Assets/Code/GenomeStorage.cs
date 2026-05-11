using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GenomeStorage", menuName = "GenomeStorage", order = 0)]
public class GenomeStorage : ScriptableObject {
    public List<GenomeData> genomes;
}
