using System.Collections.Generic;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 양방향 A* 가 읽는 그래프 — 원본의 그래프 객체 약속(neighbors · cost · isTraversable · posX/posY) `[소스] Cq3gr8XO.js:expandNode(e,t){`.
    /// </summary>
    public interface IFrontWarsAStarGraph<TNode>
    {
        /// <summary><paramref name="into"/> 를 비우고 이웃을 원본 순서대로 채운다. 채운 수를 돌려준다.</summary>
        int NeighborsInto(TNode node, List<TNode> into);

        /// <summary><paramref name="node"/> 로 들어가는 비용.</summary>
        double Cost(TNode node);

        /// <summary><paramref name="from"/> 에서 <paramref name="to"/> 로 갈 수 있나. 목표 노드로는 이 판정 없이 들어간다.</summary>
        bool IsTraversable(TNode from, TNode to);

        int PosX(TNode node);

        int PosY(TNode node);
    }
}
