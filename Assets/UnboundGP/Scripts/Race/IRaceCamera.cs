namespace UnboundGP.Race
{
    /// <summary>
    /// レースカメラの共通インターフェース。
    /// 三人称(ChaseCamera)と一人称(CockpitCamera)を RaceManager から同一に扱い、
    /// モードに応じて差し替えられるようにする。
    /// </summary>
    public interface IRaceCamera
    {
        /// <summary>注視・追従するマシンを設定する。</summary>
        void SetTarget(CarController car);
    }
}
