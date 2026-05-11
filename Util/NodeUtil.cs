// Util/NodeUtil.cs
//
// Native AtkNode helpers for the v0.4.0 StatusPanelInjector.
//
// CloneNode<T> is a verbatim port from CharacterPanelRefined (MIT licensed).
// Original author: Caraxi / CPR contributors.
// Source: ffxiv-characterstatus-refined / CharacterPanelRefined / Util.cs
// License text: see LICENSES/CharacterPanelRefined-MIT.txt
//
// The pattern: allocate game UI memory at sizeof(T), byte-copy the source
// node's struct into it, then null out the sibling/child links so the clone
// is an orphan ready to be re-parented. The game's UldManager handles
// cleanup when the owning addon is torn down — we don't free the allocation
// ourselves, matching CPR's behavior.

using System;
using System.Runtime.InteropServices;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace GearGoblin.Util;

internal static class NodeUtil
{
    /// <summary>
    /// Clone a native AtkResNode (or subtype) into a fresh game UI allocation.
    /// Returned node is an orphan with all sibling/child links nulled.
    /// </summary>
    /// <remarks>
    /// Ported verbatim from CharacterPanelRefined (MIT). Do not modify the body
    /// without updating attribution in LICENSES/CharacterPanelRefined-MIT.txt.
    /// </remarks>
    public static unsafe T* CloneNode<T>(T* original) where T : unmanaged
    {
        var size = sizeof(T);
        var allocation = MemoryHelper.GameAllocateUi((ulong)size);
        var bytes = new byte[size];
        Marshal.Copy(new IntPtr(original), bytes, 0, bytes.Length);
        Marshal.Copy(bytes, 0, allocation, bytes.Length);

        var newNode = (AtkResNode*)allocation;
        newNode->ChildNode       = null;
        newNode->ChildCount      = 0;
        newNode->PrevSiblingNode = null;
        newNode->NextSiblingNode = null;
        return (T*)newNode;
    }

    /// <summary>
    /// Allocate a fresh string buffer for an AtkTextNode that came out of CloneNode.
    /// The clone shares the source node's <c>NodeText.StringPtr</c>, which means
    /// writing to it would clobber the original. Always call this immediately
    /// after cloning a text node, before <c>SetText</c>.
    /// </summary>
    public static unsafe void AllocateFreshTextBuffer(AtkTextNode* node)
    {
        node->NodeText.StringPtr =
            (byte*)MemoryHelper.GameAllocateUi((ulong)node->NodeText.BufSize);
    }
}
