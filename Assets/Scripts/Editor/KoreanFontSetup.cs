#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using TMPro;

// TMP 기본 폰트(LiberationSans SDF)에 한글 글리프가 없어 네모(▢)로 표기되는 문제 해결용.
// 상단 메뉴: Tools > Setup Korean Font
//
// 동작:
//  1. Assets/Fonts/ 폴더가 없으면 생성.
//  2. 해당 폴더에 TTF/OTF가 있으면 그 중 하나를 선택.
//  3. 없으면 Windows 시스템 폰트(맑은 고딕 등)를 복사해옴.
//  4. 그 폰트로 TMP Dynamic SDF 폰트 에셋을 생성(기존 에셋이 있으면 재사용).
//  5. LiberationSans SDF의 fallbackFontAssetTable에 등록.
//     이후 TMP는 한글 글리프가 나올 때마다 이 폰트에서 동적으로 렌더링.
public static class KoreanFontSetup
{
    private const string FontsFolder    = "Assets/Fonts";
    private const string BaseTmpFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    // Windows에 기본 포함된 한글 폰트 후보 (앞의 것일수록 우선).
    private static readonly string[] WindowsFontCandidates =
    {
        @"C:\Windows\Fonts\malgun.ttf",      // 맑은 고딕
        @"C:\Windows\Fonts\malgunbd.ttf",    // 맑은 고딕 Bold
        @"C:\Windows\Fonts\NanumGothic.ttf", // 나눔고딕 (설치 시)
    };

    [MenuItem("Tools/Setup Korean Font")]
    public static void Setup()
    {
        if (!AssetDatabase.IsValidFolder(FontsFolder))
            AssetDatabase.CreateFolder("Assets", "Fonts");

        // 1) 기존 폰트 탐색, 없으면 시스템 폰트 복사
        string ttfPath = FindExistingFont();
        if (string.IsNullOrEmpty(ttfPath))
            ttfPath = TryCopyWindowsKoreanFont();

        if (string.IsNullOrEmpty(ttfPath))
        {
            EditorUtility.DisplayDialog(
                "Setup Korean Font",
                "한글 글꼴(.ttf 또는 .otf)을 찾지 못했습니다.\n\n" +
                "해결 방법:\n" +
                "1. Noto Sans KR, 나눔고딕 등 한글 TTF를 다운받아\n" +
                "2. Assets/Fonts/ 폴더에 넣고\n" +
                "3. 이 메뉴를 다시 실행하세요.",
                "확인");
            return;
        }

        Font font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        if (font == null)
        {
            EditorUtility.DisplayDialog(
                "Setup Korean Font",
                $"Font 에셋 로드 실패:\n{ttfPath}\n\nUnity의 임포트가 끝난 뒤 다시 시도하세요.",
                "확인");
            return;
        }

        // 2) TMP Dynamic SDF 폰트 에셋 생성/재사용
        string tmpFontPath = FontsFolder + "/" + Path.GetFileNameWithoutExtension(ttfPath) + " SDF.asset";
        TMP_FontAsset tmpFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(tmpFontPath);
        if (tmpFont == null)
        {
            tmpFont = TMP_FontAsset.CreateFontAsset(
                font,
                samplingPointSize: 90,
                atlasPadding: 9,
                renderMode: GlyphRenderMode.SDFAA,
                atlasWidth: 1024,
                atlasHeight: 1024,
                atlasPopulationMode: AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: true);
            tmpFont.name = Path.GetFileNameWithoutExtension(tmpFontPath);
            AssetDatabase.CreateAsset(tmpFont, tmpFontPath);
        }

        // 3) LiberationSans SDF의 fallback 체인에 등록
        TMP_FontAsset baseFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BaseTmpFontPath);
        if (baseFont == null)
        {
            EditorUtility.DisplayDialog(
                "Setup Korean Font",
                $"기본 TMP 폰트를 찾을 수 없습니다:\n{BaseTmpFontPath}\n\n" +
                "Window > TextMeshPro > Import TMP Essential Resources 를 먼저 실행하세요.",
                "확인");
            return;
        }

        if (baseFont.fallbackFontAssetTable == null)
            baseFont.fallbackFontAssetTable = new List<TMP_FontAsset>();

        bool alreadyLinked = baseFont.fallbackFontAssetTable.Contains(tmpFont);
        if (!alreadyLinked)
        {
            baseFont.fallbackFontAssetTable.Add(tmpFont);
            EditorUtility.SetDirty(baseFont);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Setup Korean Font",
            $"한글 글꼴이 fallback으로 연결되었습니다:\n  {tmpFont.name}\n\n" +
            (alreadyLinked ? "(이미 연결되어 있었음)\n\n" : "") +
            "▶ Play 해서 UI의 한글이 정상 표시되는지 확인하세요.\n" +
            "표시되지 않는다면 씬을 다시 로드하거나 TMP 오브젝트를 재생성해 보세요.",
            "확인");
    }

    // Assets/Fonts/ 안에서 .ttf 또는 .otf 파일 하나를 찾는다.
    private static string FindExistingFont()
    {
        string[] guids = AssetDatabase.FindAssets("t:Font", new[] { FontsFolder });
        foreach (var g in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            string ext = Path.GetExtension(p).ToLowerInvariant();
            if (ext == ".ttf" || ext == ".otf") return p;
        }
        return null;
    }

    // Windows에 기본 설치된 한글 TTF를 Assets/Fonts/ 로 복사.
    // 성공 시 Unity 에셋 경로(forward slash) 반환.
    private static string TryCopyWindowsKoreanFont()
    {
        foreach (var src in WindowsFontCandidates)
        {
            if (!File.Exists(src)) continue;

            string fname = Path.GetFileName(src);
            string dst = FontsFolder + "/" + fname;
            string dstFull = Path.GetFullPath(dst);

            if (!File.Exists(dstFull))
            {
                try { File.Copy(src, dstFull); }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[KoreanFontSetup] 폰트 복사 실패({src}): {e.Message}");
                    continue;
                }
                AssetDatabase.ImportAsset(dst);
            }
            return dst;
        }
        return null;
    }
}
#endif
