namespace UnboundGP.Core
{
    /// <summary>
    /// レースの開催カテゴリ。
    /// UNBOUND GP の世界では「マシンのレギュレーション」が存在しないため、
    /// 唯一残された変数は“誰が運転するか”である。
    /// </summary>
    public enum GameMode
    {
        /// <summary>人間ドライバーが搭乗する。人体のG限界という“見えないレギュレーション”が存在する。</summary>
        HumanGP,

        /// <summary>AIドライバーが搭乗する。人間という制約が消えた世界で、マシンは設計上の限界まで走る。</summary>
        MachineGP,
    }
}
