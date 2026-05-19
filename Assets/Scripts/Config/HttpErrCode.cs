namespace KiHan.Config
{
    public enum HttpErrCode : int
    {
        Ok = 0,
        ApiBadReq = -1000,
        ApiInternalError = -1001,
        ApiBadPassword = -1100,
        ApiBadToken = -1101,
        ApiDbError = -1200,
        DbExists = -1201,
        DbNotExists = -1202
    }
}