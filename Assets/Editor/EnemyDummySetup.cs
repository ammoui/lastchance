using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Одноразовая настройка задачи #24: создаёт префаб Dummy,
/// добавляет NavMeshSurface на пол сцены Room, запекает NavMesh
/// и расставляет трёх болванчиков. Меню: Tools → Enemy Dummies → Setup Room Scene.
/// </summary>
public static class EnemyDummySetup
{
    private const string PrefabPath = "Assets/Prefabs/Dummy.prefab";
    private const string ScenePath = "Assets/Scenes/Room.unity";

    [MenuItem("Tools/Enemy Dummies/Setup Room Scene")]
    public static void Setup()
    {
        GameObject prefab = CreateDummyPrefab();
        SetupScene(prefab);
        AssetDatabase.SaveAssets();
        Debug.Log("[EnemyDummySetup] Готово: префаб создан, NavMesh запечён, 3 болванчика расставлены.");
    }

    private static GameObject CreateDummyPrefab()
    {
        // Цилиндр примерно в рост человека: 1.8 м высотой, 0.5 м в диаметре.
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Dummy";
        go.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);

        // Коллидер не нужен: столкновениями управляет NavMeshAgent,
        // иначе болванчик будет физически толкать риг игрока.
        Object.DestroyImmediate(go.GetComponent<CapsuleCollider>());

        NavMeshAgent agent = go.AddComponent<NavMeshAgent>();
        agent.height = 1.8f;
        agent.radius = 0.3f;

        go.AddComponent<EnemyDummy>();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);
        return prefab;
    }

    private static void SetupScene(GameObject prefab)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // NavMeshSurface на пол
        GameObject floor = GameObject.Find("Floor");
        if (floor == null)
        {
            Debug.LogError("[EnemyDummySetup] Объект 'Floor' не найден в сцене Room.");
            return;
        }

        NavMeshSurface surface = floor.GetComponent<NavMeshSurface>();
        if (surface == null)
            surface = floor.AddComponent<NavMeshSurface>();

        // Собираем геометрию всей сцены: стены и колонны станут препятствиями.
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
        surface.BuildNavMesh();

        // Три болванчика в разных местах комнаты (по углам от центра пола).
        Bounds b = floor.GetComponent<Renderer>().bounds;
        Vector3[] offsets =
        {
            new Vector3(b.extents.x * 0.5f, 0f, b.extents.z * 0.5f),
            new Vector3(-b.extents.x * 0.5f, 0f, b.extents.z * 0.5f),
            new Vector3(0f, 0f, -b.extents.z * 0.6f),
        };

        foreach (Vector3 offset in offsets)
        {
            Vector3 probe = b.center + offset;
            if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                Debug.LogWarning($"[EnemyDummySetup] Не нашёл NavMesh рядом с {probe}, пропускаю точку.");
                continue;
            }
            GameObject dummy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            dummy.transform.position = hit.position;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
