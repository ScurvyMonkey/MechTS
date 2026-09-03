using MechTS.AI;
using MechTS.Battle;
using MechTS.Economy;
using MechTS.UI;
using MechTS.Units;
using MechTS.Vision;
using UnityEngine;
using UnityEngine.AI;

namespace MechTS.Core
{
    /// <summary>
    /// Creates and persists MechTS's manager singletons at startup. All manager
    /// singletons are created here — never elsewhere — per the project's convention.
    /// Place one instance of this component in the game's initial scene.
    /// </summary>
    public class Bootstrapper : MonoBehaviour
    {
        [SerializeField] private MissionEconomyConfig _missionConfig;
        [SerializeField] private CameraConfig _cameraConfig;
        [SerializeField] private TechTreeConfig _techTreeConfig;
        [SerializeField] private AudioCategoryConfig _audioCategoryConfig;
        [SerializeField] private EnemyAIEconomyConfig _enemyAIEconomyConfig;

        /// <summary>
        /// The Flying NavMesh Agent Type's baked data (issue #40), wired directly in the
        /// Inspector — unlike the default (Humanoid) agent's NavMesh, which Unity loads
        /// automatically because it's associated with the scene by convention, a second
        /// agent type's <see cref="NavMeshData"/> asset has no automatic loading path and
        /// needs an explicit <see cref="NavMesh.AddNavMeshData(NavMeshData)"/> call — this is
        /// that call. Left unassigned is a safe no-op (flying units simply can't path until
        /// one is wired), so this doesn't break flat/no-elevation missions that never bake a
        /// Flying layer at all.
        /// </summary>
        [SerializeField] private NavMeshData _flyingNavMeshData;

        /// <summary>
        /// Instantiates every manager singleton and marks each to persist across scene loads.
        /// </summary>
        private void Awake()
        {
            CreateManager<GameManager>("GameManager");
            CreateManager<UnitManager>("UnitManager");
            CreateManager<EconomyManager>("EconomyManager").Initialize(_missionConfig);
            CreateManager<BattleManager>("BattleManager");
            CreateManager<UIManager>("UIManager");
            CreateManager<CameraManager>("CameraManager").Initialize(_cameraConfig);
            CreateManager<AudioManager>("AudioManager").Initialize(_audioCategoryConfig);
            CreateManager<TechManager>("TechManager").Initialize(_techTreeConfig);
            CreateManager<VisionManager>("VisionManager");
            CreateManager<EnemyAIController>("EnemyAIController").Initialize(_enemyAIEconomyConfig);

            if (_flyingNavMeshData != null)
            {
                NavMesh.AddNavMeshData(_flyingNavMeshData);
            }
        }

        /// <summary>
        /// Creates a manager of type <typeparamref name="T"/> on a new persistent GameObject.
        /// </summary>
        /// <param name="name">The name to give the manager's GameObject, for editor readability.</param>
        private static T CreateManager<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            var manager = go.AddComponent<T>();
            DontDestroyOnLoad(go);
            return manager;
        }
    }
}
