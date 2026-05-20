using UnityEngine;

/// <summary>
/// Дефолтные боевые клипы (Resources/CombatSfxLibrary). Подставляются, если на Weapon/Tank поля пустые.
/// </summary>
[CreateAssetMenu(fileName = "CombatSfxLibrary", menuName = "CyberGambit/Combat SFX Library")]
public class CombatSfxLibrary : ScriptableObject
{
    [Header("Guardian (ракетчик)")]
    public AudioClip guardianFire;
    public AudioClip guardianReload;

    [Header("Unit (пехота: Pawn, Bishop, Guardian…)")]
    public AudioClip unitMove;

    [Header("Tank")]
    public AudioClip tankFire;
    public AudioClip tankReload;
    public AudioClip tankMove;

    private static CombatSfxLibrary instance;

    public static CombatSfxLibrary Instance
    {
        get
        {
            if (instance == null)
                instance = Resources.Load<CombatSfxLibrary>("CombatSfxLibrary");
            return instance;
        }
    }

    public AudioClip GetGuardianFire() => guardianFire;
    public AudioClip GetGuardianReload() => guardianReload;
    public AudioClip GetUnitMove() => unitMove;
    public AudioClip GetTankFire() => tankFire;
    public AudioClip GetTankReload() => tankReload;
    public AudioClip GetTankMove() => tankMove;
}
