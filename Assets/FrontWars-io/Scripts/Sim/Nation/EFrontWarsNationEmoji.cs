namespace JinHyung.FrontWarsIo
{
    /// <summary>동맹 판단 때 보내는 이모지 종류 `[소스] Cq3gr8XO.js:sendAllianceEmoji(e,t){let n={confused:Au,scaredOfThreat:Lu,love:ku,handshake:zu}`.</summary>
    public enum EFrontWarsNationAllianceEmoji
    {
        Confused,       // ❓ 🤡
        ScaredOfThreat, // (🙏) 🥺
        Love,           // ❤️ 😊 🥰
        Handshake       // 🤝
    }

    /// <summary>동맹 돕기 때 보내는 이모지 종류 `[소스] Cq3gr8XO.js:sendAssistEmoji(e,t){let n={accept:bu,relationTooLow:xu,targetMe:Su,targetAlly:Cu}`.</summary>
    public enum EFrontWarsNationAssistEmoji
    {
        Accept,         // 👍 🤝 🎯
        RelationTooLow, // 🥱 🤦‍♂️
        TargetMe,       // 🥺 💀
        TargetAlly      // 🕊️ 👎
    }
}
