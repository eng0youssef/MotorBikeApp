using System.Data;

namespace MotorBike.DataAccess;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
