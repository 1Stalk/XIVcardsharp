using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

namespace XIVcardsharp.Interop;

public static unsafe class GUINodeUtils
{
    public static bool IsNodeVisible(AtkResNode* node) =>
        node is not null && node->IsVisible();

    public static AtkResNode* PickChildNode(AtkResNode* maybeCompNode, int childIdx, int expectedNumChildren)
    {
        if (maybeCompNode is not null && (int)maybeCompNode->Type >= 1000)
        {
            var compNode = (AtkComponentNode*)maybeCompNode;
            if (compNode->Component->UldManager.NodeListCount == expectedNumChildren && childIdx < expectedNumChildren)
            {
                return compNode->Component->UldManager.NodeList[childIdx];
            }
        }

        return null;
    }

    public static AtkResNode* PickChildNode(AtkComponentBase* compPtr, int childIdx, int expectedNumChildren)
    {
        if (compPtr is not null &&
            compPtr->UldManager.NodeListCount == expectedNumChildren &&
            childIdx < expectedNumChildren)
        {
            return compPtr->UldManager.NodeList[childIdx];
        }

        return null;
    }

    public static AtkResNode*[]? GetImmediateChildNodes(AtkResNode* node)
    {
        if (node is null || node->ChildNode is null)
        {
            return null;
        }

        var listAddr = new List<ulong> { (ulong)node->ChildNode };
        node = node->ChildNode;
        while (node->PrevSiblingNode is not null && listAddr.Count < 64)
        {
            listAddr.Add((ulong)node->PrevSiblingNode);
            node = node->PrevSiblingNode;
        }

        return ConvertToNodeArr(listAddr);
    }

    public static AtkResNode*[]? GetAllChildNodes(AtkResNode* node)
    {
        if (node is null)
        {
            return null;
        }

        var list = new List<ulong>();
        RecursiveAppendChildNodes(node, list);
        return ConvertToNodeArr(list);
    }

    private static void RecursiveAppendChildNodes(AtkResNode* node, List<ulong> listAddr, int depth = 0)
    {
        if (node is null || depth > 24)
        {
            return;
        }

        listAddr.Add((ulong)node);

        if (node->ChildNode is null)
        {
            return;
        }

        RecursiveAppendChildNodes(node->ChildNode, listAddr, depth + 1);

        var linkNode = node->ChildNode;
        var siblingCount = 0;
        while (linkNode->PrevSiblingNode is not null && siblingCount < 64)
        {
            RecursiveAppendChildNodes(linkNode->PrevSiblingNode, listAddr, depth + 1);
            linkNode = linkNode->PrevSiblingNode;
            siblingCount++;
        }
    }

    private static AtkResNode*[]? ConvertToNodeArr(List<ulong> listAddr)
    {
        if (listAddr.Count == 0)
        {
            return null;
        }

        var typedArr = new AtkResNode*[listAddr.Count];
        for (var idx = 0; idx < listAddr.Count; idx++)
        {
            typedArr[idx] = (AtkResNode*)listAddr[idx];
        }

        return typedArr;
    }

    public static AtkResNode* PickNode(AtkResNode*[]? nodes, int nodeIdx, int expectedNumNodes)
    {
        if (nodes is { Length: var length } && length == expectedNumNodes && nodeIdx < expectedNumNodes)
        {
            return nodes[nodeIdx];
        }

        return null;
    }

    public static string? GetNodeTexturePath(AtkResNode* maybeImageNode)
    {
        if (maybeImageNode is not null && maybeImageNode->Type == NodeType.Image)
        {
            var imageNode = (AtkImageNode*)maybeImageNode;
            if (imageNode->PartsList is not null && imageNode->PartId <= imageNode->PartsList->PartCount)
            {
                var textureInfo = imageNode->PartsList->Parts[imageNode->PartId].UldAsset;
                var texType = textureInfo->AtkTexture.TextureType;
                if (texType == TextureType.Resource)
                {
                    var texFileNameStdString = &textureInfo->AtkTexture.Resource->TexFileResourceHandle->ResourceHandle.FileName;
                    return texFileNameStdString->Length < 16
                        ? Marshal.PtrToStringAnsi((nint)texFileNameStdString->Buffer)
                        : Marshal.PtrToStringAnsi((nint)texFileNameStdString->BufferPtr);
                }
            }
        }

        return null;
    }

    public static string? GetNodeText(AtkResNode* maybeTextNode)
    {
        if (maybeTextNode is not null && maybeTextNode->Type == NodeType.Text)
        {
            var textNode = (AtkTextNode*)maybeTextNode;
            return Marshal.PtrToStringUTF8(new(textNode->NodeText.StringPtr));
        }

        return null;
    }
}
