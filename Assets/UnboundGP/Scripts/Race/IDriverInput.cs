namespace UnboundGP.Race
{
    /// <summary>
    /// CarController への操作入力。人間 (PlayerInputDriver) と AI (AIDriver) を
    /// 同一インターフェースで差し替えられるようにする。
    /// Human GP / Machine GP の実装上の分岐点はここと DriverProfile の2箇所だけ。
    /// </summary>
    public interface IDriverInput
    {
        /// <summary>アクセル 0〜1</summary>
        float Throttle { get; }

        /// <summary>ブレーキ 0〜1</summary>
        float Brake { get; }

        /// <summary>ステアリング -1(左)〜+1(右)</summary>
        float Steer { get; }
    }
}
