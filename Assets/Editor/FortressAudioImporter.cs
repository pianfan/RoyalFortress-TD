using UnityEditor;
using UnityEngine;

public sealed class FortressAudioImporter : AssetPostprocessor
{
    private void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith("Assets/Resources/FortressAudio/")) return;
        var importer = (AudioImporter)assetImporter;
        bool music = assetPath.Contains("/Music/");
        var settings = importer.defaultSampleSettings;
        settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
        settings.quality = .75f;
        settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
        settings.preloadAudioData = !music;
        importer.defaultSampleSettings = settings;
        importer.forceToMono = !music;
        importer.loadInBackground = false;
    }
}
