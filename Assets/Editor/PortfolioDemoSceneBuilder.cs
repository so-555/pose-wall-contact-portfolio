using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Creates a scene using only Unity primitive meshes.
/// It can be re-run from Tools > Portfolio > Rebuild Demo Scene.
/// </summary>
public static class PortfolioDemoSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/PoseWallDemo.unity";

    [MenuItem("Tools/Portfolio/Rebuild Demo Scene")]
    public static void Create()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.skybox = null;
        RenderSettings.ambientLight = new Color(0.70f, 0.75f, 0.85f);

        Material avatarMaterial = MakeMaterial("AvatarBlue", new Color(0.25f, 0.65f, 0.95f));
        Material wallMaterial = MakeMaterial("WallLight", new Color(0.75f, 0.79f, 0.84f));
        Material groundMaterial = MakeMaterial("GroundDark", new Color(0.12f, 0.18f, 0.24f));

        GameObject cameraObject = new GameObject("Main Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.08f, 0.12f, 0.19f);
        camera.fieldOfView = 47f;
        cameraObject.transform.position = new Vector3(0f, 2.25f, -9f);
        cameraObject.transform.LookAt(new Vector3(0f, 1.75f, 2f));

        GameObject lightObject = new GameObject("Directional Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.25f;
        lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

        Part(PrimitiveType.Cube, "Floor", null,
            new Vector3(0f, -0.18f, 0f), new Vector3(10f, 0.25f, 23f), groundMaterial);

        GameObject avatar = new GameObject("Demo Avatar");
        avatar.tag = "Player";
        avatar.transform.position = Vector3.zero;

        Part(PrimitiveType.Capsule, "Torso", avatar.transform,
            new Vector3(0f, 1.33f, 0f), new Vector3(0.47f, 0.43f, 0.31f), avatarMaterial);
        Part(PrimitiveType.Sphere, "Head", avatar.transform,
            new Vector3(0f, 2.05f, 0f), new Vector3(0.40f, 0.45f, 0.38f), avatarMaterial);
        Part(PrimitiveType.Cube, "Hips", avatar.transform,
            new Vector3(0f, 0.88f, 0f), new Vector3(0.45f, 0.28f, 0.33f), avatarMaterial);
        Part(PrimitiveType.Capsule, "Left Arm", avatar.transform,
            new Vector3(-0.43f, 1.43f, 0f), new Vector3(0.18f, 0.41f, 0.18f), avatarMaterial);
        Part(PrimitiveType.Capsule, "Right Arm", avatar.transform,
            new Vector3(0.43f, 1.43f, 0f), new Vector3(0.18f, 0.41f, 0.18f), avatarMaterial);
        Part(PrimitiveType.Capsule, "Left Leg", avatar.transform,
            new Vector3(-0.16f, 0.45f, 0f), new Vector3(0.20f, 0.42f, 0.22f), avatarMaterial);
        Part(PrimitiveType.Capsule, "Right Leg", avatar.transform,
            new Vector3(0.16f, 0.45f, 0f), new Vector3(0.20f, 0.42f, 0.22f), avatarMaterial);

        List<WallPassJudge.DemoContactPoint> contactPoints = new List<WallPassJudge.DemoContactPoint>();
        Marker(avatar.transform, contactPoints, "Head", HumanBodyBones.Head, 0f, 2.05f);
        Marker(avatar.transform, contactPoints, "Chest", HumanBodyBones.Chest, 0f, 1.50f);
        Marker(avatar.transform, contactPoints, "Spine", HumanBodyBones.Spine, 0f, 1.20f);
        Marker(avatar.transform, contactPoints, "Hips", HumanBodyBones.Hips, 0f, 0.86f);
        Marker(avatar.transform, contactPoints, "Left Upper Arm", HumanBodyBones.LeftUpperArm, -0.43f, 1.67f);
        Marker(avatar.transform, contactPoints, "Left Lower Arm", HumanBodyBones.LeftLowerArm, -0.43f, 1.32f);
        Marker(avatar.transform, contactPoints, "Left Hand", HumanBodyBones.LeftHand, -0.43f, 1.02f);
        Marker(avatar.transform, contactPoints, "Right Upper Arm", HumanBodyBones.RightUpperArm, 0.43f, 1.67f);
        Marker(avatar.transform, contactPoints, "Right Lower Arm", HumanBodyBones.RightLowerArm, 0.43f, 1.32f);
        Marker(avatar.transform, contactPoints, "Right Hand", HumanBodyBones.RightHand, 0.43f, 1.02f);
        Marker(avatar.transform, contactPoints, "Left Upper Leg", HumanBodyBones.LeftUpperLeg, -0.16f, 0.66f);
        Marker(avatar.transform, contactPoints, "Left Lower Leg", HumanBodyBones.LeftLowerLeg, -0.16f, 0.31f);
        Marker(avatar.transform, contactPoints, "Right Upper Leg", HumanBodyBones.RightUpperLeg, 0.16f, 0.66f);
        Marker(avatar.transform, contactPoints, "Right Lower Leg", HumanBodyBones.RightLowerLeg, 0.16f, 0.31f);

        GameObject wallManager = new GameObject("Moving Wall Manager");
        wallManager.transform.position = new Vector3(0f, 0f, 7f);
        GameObject wall = new GameObject("Wall with center opening");
        wall.transform.SetParent(wallManager.transform, false);

        Part(PrimitiveType.Cube, "Left", wall.transform,
            new Vector3(-2.65f, 1.75f, 0f), new Vector3(3.70f, 3.5f, 0.28f), wallMaterial);
        Part(PrimitiveType.Cube, "Right", wall.transform,
            new Vector3(2.65f, 1.75f, 0f), new Vector3(3.70f, 3.5f, 0.28f), wallMaterial);
        Part(PrimitiveType.Cube, "Top", wall.transform,
            new Vector3(0f, 3.08f, 0f), new Vector3(1.60f, 0.84f, 0.28f), wallMaterial);
        Part(PrimitiveType.Cube, "Bottom", wall.transform,
            new Vector3(0f, 0.05f, 0f), new Vector3(1.60f, 0.10f, 0.28f), wallMaterial);

        RandomWallLooper looper = wallManager.AddComponent<RandomWallLooper>();
        SerializedObject movingWall = new SerializedObject(looper);
        movingWall.FindProperty("speed").floatValue = 2.2f;
        movingWall.FindProperty("maxDistance").floatValue = 8.5f;
        movingWall.ApplyModifiedPropertiesWithoutUndo();

        GameObject contactObject = new GameObject("Contact Locations");
        WallPassJudge judge = contactObject.AddComponent<WallPassJudge>();
        judge.wallLooper = looper;
        judge.avatarRoot = avatar.transform;
        judge.demoContactPoints = contactPoints.ToArray();
        judge.hitLocationScreenSize = 15f;
        judge.showCollisionGizmos = true;
        judge.showHitLocations = true;

        DemoAvatarController controller = avatar.AddComponent<DemoAvatarController>();
        controller.contactSystem = judge;

        GameObject projectionObject = new GameObject("Silhouette System");
        WallSilhouetteProjection projection = projectionObject.AddComponent<WallSilhouetteProjection>();
        SerializedObject projectionFields = new SerializedObject(projection);
        projectionFields.FindProperty("wallLooper").objectReferenceValue = looper;
        projectionFields.FindProperty("avatarRoot").objectReferenceValue = avatar.transform;
        projectionFields.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("Portfolio scene generated: " + ScenePath);
    }

    [MenuItem("Tools/Portfolio/Build Windows Demo")]
    public static void BuildWindows()
    {
        System.IO.Directory.CreateDirectory("Builds");
        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Builds/PoseWallDemo.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };
        var report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            throw new System.Exception("Windows demo build failed: " + report.summary.result);
        }
        Debug.Log("Portfolio Windows demo built: " + options.locationPathName);
    }

    private static GameObject Part(PrimitiveType type, string name, Transform parent,
        Vector3 position, Vector3 scale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        if (parent != null) part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = material;
        return part;
    }

    private static void Marker(Transform root, List<WallPassJudge.DemoContactPoint> contacts,
        string label, HumanBodyBones bone, float x, float y)
    {
        GameObject point = new GameObject("Sample - " + label);
        point.transform.SetParent(root, false);
        point.transform.localPosition = new Vector3(x, y, 0f);
        contacts.Add(new WallPassJudge.DemoContactPoint { bodyPart = bone, anchor = point.transform });
    }

    private static Material MakeMaterial(string name, Color color)
    {
        string folder = "Assets/Demo/Materials";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/Demo", "Materials");
        string path = folder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        EditorUtility.SetDirty(material);
        return material;
    }
}
