using Medabots.Rom.Metadata;

namespace Medabots.Rom.Projects;

public sealed partial class RomHackProjectApplicator
{
    private static void ApplyPendingActions(RomHackProject project, RomHackSession session)
    {
        session.ApplyPatches(project.PendingActions);
    }

    private void ApplyMessagePatches(RomHackProject project, RomHackSession session, MedabotsRomTextProfile? profile)
    {
        if (project.MessagePatches.Count == 0)
        {
            return;
        }

        var resolvedProfile = profile ?? throw new InvalidOperationException("The project does not define a known text profile.");
        _textPatcher.Apply(session, resolvedProfile.TextPointerTableOffset, resolvedProfile.TextDumpOffset, project.MessagePatches);
    }

    private void ApplyEventScriptPatches(RomHackProject project, RomHackSession session, MedabotsRomTextProfile? profile)
    {
        if (project.EventScriptPatches.Count == 0 && project.DeletedEventScriptIds.Count == 0)
        {
            return;
        }

        var resolvedProfile = profile ?? throw new InvalidOperationException("The project does not define a known text profile.");
        resolvedProfile = EnsureExpandedEventScriptDatabase(project, session, resolvedProfile);
        foreach (var deletedEventId in project.DeletedEventScriptIds.Distinct().OrderBy(id => id))
        {
            _eventInstructionPatcher.RewriteEvent(session, resolvedProfile, deletedEventId, [MedabotsRomSchema.EventEndOpcode], $"Delete project event patch {deletedEventId}");
        }

        foreach (var patch in project.EventScriptPatches)
        {
            _eventInstructionPatcher.RewriteEvent(session, resolvedProfile, patch.EventId, patch.ScriptBytes, $"Apply project event patch {patch.EventId}");
        }
    }

    private MedabotsRomTextProfile EnsureExpandedEventScriptDatabase(RomHackProject project, RomHackSession session, MedabotsRomTextProfile profile)
    {
        var requiredEventCount = Math.Max(
            profile.EventCount,
            Math.Max(
                project.EventScriptPatches.Count == 0 ? 0 : project.EventScriptPatches.Max(patch => patch.EventId + 1),
                project.DeletedEventScriptIds.Count == 0 ? 0 : project.DeletedEventScriptIds.Max(id => id + 1)));
        if (requiredEventCount <= profile.EventCount)
        {
            return profile;
        }

        var originalDatabaseBaseOffset = profile.EventTableOffset - MedabotsRomSchema.EventBankBaseAddress;
        var originalPointerTableLength = profile.EventCount * 2;
        var originalBankTableOffset = profile.EventTableOffset + originalPointerTableLength;
        var newDatabaseBaseOffset = AlignUp(Math.Max(session.RomFile.Length, 0x800000), 4);
        var newPointerTableOffset = newDatabaseBaseOffset + MedabotsRomSchema.EventBankBaseAddress;
        var newBankTableRelativeOffset = MedabotsRomSchema.EventBankBaseAddress + (requiredEventCount * 2);
        var newBankTableOffset = newDatabaseBaseOffset + newBankTableRelativeOffset;
        var newDatabaseLength = newBankTableRelativeOffset + requiredEventCount;
        var databaseBytes = new byte[newDatabaseLength];

        databaseBytes[0] = 0x00;
        databaseBytes[1] = 0x40;
        databaseBytes[2] = (byte)(newBankTableRelativeOffset & 0xFF);
        databaseBytes[3] = (byte)((newBankTableRelativeOffset >> 8) & 0xFF);
        Array.Copy(session.RomFile.Data, profile.EventTableOffset, databaseBytes, MedabotsRomSchema.EventBankBaseAddress, originalPointerTableLength);
        Array.Copy(session.RomFile.Data, originalBankTableOffset, databaseBytes, newBankTableRelativeOffset, profile.EventCount);
        session.ApplyPatch(RomPatchAction.Create(newDatabaseBaseOffset, databaseBytes, $"Expand event script database to {requiredEventCount} slots"));

        var originalDatabaseBaseAddress = GbaPointer.ToRomAddress(originalDatabaseBaseOffset);
        var newDatabaseBaseAddress = GbaPointer.ToRomAddress(newDatabaseBaseOffset);
        var oldPointerBytes = BitConverter.GetBytes(originalDatabaseBaseAddress);
        var newPointerBytes = BitConverter.GetBytes(newDatabaseBaseAddress);
        var romData = session.RomFile.Data;
        for (var offset = 0; offset <= romData.Length - 4; offset++)
        {
            if (romData[offset] == oldPointerBytes[0] &&
                romData[offset + 1] == oldPointerBytes[1] &&
                romData[offset + 2] == oldPointerBytes[2] &&
                romData[offset + 3] == oldPointerBytes[3])
            {
                session.ApplyPatch(RomPatchAction.Create(offset, newPointerBytes, $"Repoint event script database literal at 0x{offset:X}"));
            }
        }

        var addresses = profile.Addresses;
        return new MedabotsRomTextProfile(
            profile.Id,
            profile.Name,
            profile.HeaderSignature,
            new MedabotsRomAddresses(
                addresses.TextPointerTableOffset,
                addresses.TextDumpOffset,
                addresses.StarterOffset,
                addresses.BattlePointerTableOffset,
                addresses.BattleCount,
                newPointerTableOffset,
                requiredEventCount));
    }

    private static int AlignUp(int value, int alignment)
    {
        var remainder = value % alignment;
        return remainder == 0 ? value : value + (alignment - remainder);
    }

    private void ApplyMapEntitySpawnPatches(RomHackProject project, RomHackSession session)
    {
        if (project.MapEntitySpawnPatches.Count == 0)
        {
            return;
        }

        foreach (var patch in project.MapEntitySpawnPatches.OrderBy(patch => patch.MapId))
        {
            _mapOverlayPatcher.RewriteEntitySpawns(session, patch, $"Apply map {patch.MapId} entity spawn patch");
        }
    }

    private void ApplyMapWarpPatches(RomHackProject project, RomHackSession session)
    {
        if (project.MapWarpPatches.Count == 0)
        {
            return;
        }

        foreach (var patch in project.MapWarpPatches.OrderBy(patch => patch.MapId))
        {
            _mapOverlayPatcher.RewriteWarps(session, patch, $"Apply map {patch.MapId} warp patch");
        }
    }

    private void ApplyMapCollisionPatches(RomHackProject project, RomHackSession session)
    {
        if (project.MapCollisionPatches.Count == 0)
        {
            return;
        }

        foreach (var patch in project.MapCollisionPatches.OrderBy(patch => patch.MapId))
        {
            _mapOverlayPatcher.RewriteCollisionAttributes(session, patch, $"Apply map {patch.MapId} collision patch");
        }
    }

    private void ApplyMapLayerPatches(RomHackProject project, RomHackSession session)
    {
        if (project.MapLayerPatches.Count == 0)
        {
            return;
        }

        foreach (var patch in project.MapLayerPatches.OrderBy(patch => patch.MapId).ThenBy(patch => patch.LayerIndex))
        {
            _mapLayerPatcher.RewriteLayer(session, patch, $"Apply map {patch.MapId} layer {patch.LayerIndex + 1} patch");
        }
    }

    private void ApplyMapEncounterPatches(RomHackProject project, RomHackSession session)
    {
        if (project.MapEncounterPatches.Count == 0)
        {
            return;
        }

        var encounterTableOffset = _encounterTableReader.FindTableOffset(session.RomFile);
        foreach (var patch in project.MapEncounterPatches.OrderBy(patch => patch.MapId))
        {
            var offset = encounterTableOffset + (patch.MapId * Encounters.EncounterTableReader.EncounterSize);
            session.ApplyPatch(RomPatchAction.Create(offset, [patch.Battle1, patch.Battle2, patch.Battle3, patch.Battle4], $"Apply map {patch.MapId} encounter patch"));
        }
    }

    private static void ApplyMapEncounterStatePatches(RomHackProject project, RomHackSession session)
    {
        if (project.MapEncounterStatePatches.Count == 0)
        {
            return;
        }

        foreach (var patch in project.MapEncounterStatePatches.OrderBy(patch => patch.MapId))
        {
            var offset = MedabotsRomSchema.MapEncounterSettingsTableOffset + (patch.MapId * 8);
            session.ApplyPatch(RomPatchAction.Create(offset, [patch.EncounterEnabledByte], $"Apply map {patch.MapId} encounter enable patch"));
        }
    }

    private static void ApplyMapMusicPatches(RomHackProject project, RomHackSession session)
    {
        if (project.MapMusicPatches.Count == 0)
        {
            return;
        }

        foreach (var patch in project.MapMusicPatches.OrderBy(patch => patch.MapId))
        {
            var offset = MedabotsRomSchema.MapMusicTableOffset + patch.MapId;
            session.ApplyPatch(RomPatchAction.Create(offset, [patch.MusicId], $"Apply map {patch.MapId} music patch"));
        }
    }

    private void ApplyMapEventObjectResourcePatches(RomHackProject project, RomHackSession session)
    {
        if (project.MapEventObjectResourcePatches.Count == 0)
        {
            return;
        }

        foreach (var patch in project.MapEventObjectResourcePatches.OrderBy(patch => patch.MapId))
        {
            _mapOverlayPatcher.RewriteEventObjectResources(session, patch, $"Apply map {patch.MapId} sprite slot patch");
        }
    }

    private void ApplySpriteEdits(RomHackProject project, RomHackSession session)
    {
        foreach (var sprite in project.OverworldSpriteEdits.OrderBy(asset => asset.SpriteId))
        {
            _imageAssetPatcher.ApplySpriteSmart(session, sprite);
        }

        foreach (var portrait in project.PortraitEdits.OrderBy(asset => asset.CharacterId).ThenBy(asset => asset.PortraitIndex))
        {
            _imageAssetPatcher.ApplyPortraitSmart(session, portrait);
        }

        foreach (var component in project.BattleCompositeSpriteEdits.OrderBy(asset => asset.MedabotId).ThenBy(asset => asset.ComponentIndex))
        {
            _imageAssetPatcher.ApplyBattleCompositeSpriteComponentSmart(session, component);
        }

        foreach (var asset in project.LargePartDisplayEdits.OrderBy(entry => entry.PartId).ThenBy(entry => entry.VariantSelector))
        {
            _imageAssetPatcher.ApplyLargePartDisplaySmart(session, asset);
        }
    }

    private void ApplyBattleEdits(RomHackProject project, RomHackSession session)
    {
        foreach (var battle in project.BattleEdits.OrderBy(edit => edit.Id))
        {
            _battlePatcher.Apply(session, battle);
        }
    }

    private void ApplyPartEdits(RomHackProject project, RomHackSession session)
    {
        foreach (var part in project.PartEdits.OrderBy(edit => edit.Id))
        {
            _partPatcher.Apply(session, part);
        }
    }
}
