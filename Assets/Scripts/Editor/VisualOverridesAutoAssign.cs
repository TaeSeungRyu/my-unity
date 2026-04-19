#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Assets/Models/ 폴더에 있는 FBX/GLB/OBJ/Prefab 파일명을 키워드로 매칭해서
// VisualOverrides의 각 슬롯(Player/Walker/...)에 자동 연결한다.
//
// 사용 순서:
//   1. Kenney.nl 같은 곳에서 받은 모델(.fbx, .glb)을 Assets/Models/ 에 드롭
//   2. 상단 메뉴: Tools > Auto-Assign Models to VisualOverrides
//   3. 다이얼로그에 매칭 리포트가 뜸. 매칭 안 된 슬롯은 Inspector에서 수동 지정
//   4. Tools > Setup Game Scene 실행 → 외형 교체된 씬이 생성됨
public static class VisualOverridesAutoAssign
{
    private const string ModelsFolder   = "Assets/Models";
    private const string OverridesPath  = "Assets/Prefabs/VisualOverrides.asset";

    // 슬롯별 키워드 후보. 배열의 앞쪽일수록 우선순위 높음.
    // Kenney 패키지(Blocky Characters, Space Kit, Alien UFO Pack 등)의
    // 파일명 패턴을 염두에 두고 선정했다.
    private static readonly (string slot, string[] keywords)[] SlotKeywords =
    {
        ("player", new[] { "player", "hero", "character-male-a", "character_male_a", "character-a", "male-a", "male" }),
        ("walker", new[] { "walker", "zombie", "skeleton" }),
        ("chaser", new[] { "chaser", "demon", "ghost", "enemy" }),
        ("jumper", new[] { "jumper", "slime", "blob", "bunny", "rabbit", "frog" }),
        ("flyer",  new[] { "ufo", "flyer", "craft-speeder", "speeder", "alien-ship", "spaceship", "ship", "alien" }),
        ("tank",   new[] { "tank", "boss", "robot", "heavy", "mech" }),
    };

    [MenuItem("Tools/Auto-Assign Models to VisualOverrides")]
    public static void AutoAssign()
    {
        if (!AssetDatabase.IsValidFolder(ModelsFolder))
        {
            if (EditorUtility.DisplayDialog(
                    "Auto-Assign",
                    "'Assets/Models/' 폴더가 없습니다.\n" +
                    "Kenney.nl 등에서 받은 FBX/GLB 파일을 이 폴더에 넣고 다시 시도하세요.\n\n" +
                    "지금 빈 폴더를 만들까요?",
                    "폴더 생성", "취소"))
            {
                AssetDatabase.CreateFolder("Assets", "Models");
            }
            return;
        }

        // Assets/Models 하위에서 GameObject 에셋(FBX/GLB/OBJ/Prefab) 수집
        string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { ModelsFolder });
        List<Candidate> candidates = new List<Candidate>();
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;
            // 파일명 기반 매칭이 가장 직관적이므로 path에서 파일명만 추출해 비교
            string fileName = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            candidates.Add(new Candidate { name = fileName, asset = go, path = path });
        }

        if (candidates.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Auto-Assign",
                "'Assets/Models/' 안에서 FBX/GLB/OBJ/Prefab을 찾지 못했습니다.\n" +
                "모델 파일을 넣은 뒤 Unity가 임포트를 끝내길 기다리고 다시 시도하세요.",
                "확인");
            return;
        }

        VisualOverrides vo = LoadOrCreateOverrides();
        Undo.RecordObject(vo, "Auto-Assign Models to VisualOverrides");

        List<string> report = new List<string>();
        HashSet<string> usedAssetPaths = new HashSet<string>();

        foreach (var (slot, keywords) in SlotKeywords)
        {
            // 이미 다른 슬롯이 쓴 파일은 중복 할당하지 않도록 제외
            var pool = candidates.Where(c => !usedAssetPaths.Contains(c.path)).ToList();
            var match = FindBestMatch(pool, keywords);

            AssignSlot(vo, slot, match.asset);

            if (match.asset != null)
            {
                usedAssetPaths.Add(match.path);
                report.Add($"  [O] {slot,-8} <- {match.asset.name}   (키워드: '{match.keyword}')");
            }
            else
            {
                report.Add($"  [X] {slot,-8} <- (매칭 없음)");
            }
        }

        EditorUtility.SetDirty(vo);
        AssetDatabase.SaveAssets();

        Selection.activeObject = vo;
        EditorGUIUtility.PingObject(vo);

        string summary =
            $"VisualOverrides 자동 연결 완료 ({candidates.Count}개 후보 중 매칭):\n\n" +
            string.Join("\n", report) +
            "\n\n- 매칭되지 않은 슬롯은 Inspector에서 직접 드래그해 넣으세요." +
            "\n- 완료되면 'Tools > Setup Game Scene'을 다시 실행하면 외형이 적용됩니다.";

        EditorUtility.DisplayDialog("Auto-Assign", summary, "확인");
        Debug.Log("[VisualOverridesAutoAssign]\n" + summary);
    }

    private struct Candidate
    {
        public string name;      // 소문자 파일명 (확장자 제외)
        public GameObject asset; // FBX/GLB/Prefab을 GameObject로 참조
        public string path;      // 에셋 경로
    }

    private struct Match
    {
        public GameObject asset;
        public string path;
        public string keyword;
    }

    // 키워드 목록을 앞에서부터 탐색. 파일명에 키워드가 포함된 첫 후보를 반환.
    private static Match FindBestMatch(List<Candidate> candidates, string[] keywords)
    {
        foreach (var kw in keywords)
        {
            string needle = kw.ToLowerInvariant();
            foreach (var c in candidates)
            {
                // 하이픈/언더스코어 모두 수용하기 위해 양쪽에서 구분자 제거 후 비교
                string a = c.name.Replace("_", "").Replace("-", "");
                string b = needle.Replace("_", "").Replace("-", "");
                if (a.Contains(b))
                    return new Match { asset = c.asset, path = c.path, keyword = kw };
            }
        }
        return new Match { asset = null, path = null, keyword = null };
    }

    private static void AssignSlot(VisualOverrides vo, string slot, GameObject asset)
    {
        switch (slot)
        {
            case "player": vo.playerVisual = asset; break;
            case "walker": vo.walkerVisual = asset; break;
            case "chaser": vo.chaserVisual = asset; break;
            case "jumper": vo.jumperVisual = asset; break;
            case "flyer":  vo.flyerVisual  = asset; break;
            case "tank":   vo.tankVisual   = asset; break;
        }
    }

    private static VisualOverrides LoadOrCreateOverrides()
    {
        var vo = AssetDatabase.LoadAssetAtPath<VisualOverrides>(OverridesPath);
        if (vo != null) return vo;

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        vo = ScriptableObject.CreateInstance<VisualOverrides>();
        AssetDatabase.CreateAsset(vo, OverridesPath);
        AssetDatabase.SaveAssets();
        return vo;
    }
}
#endif
