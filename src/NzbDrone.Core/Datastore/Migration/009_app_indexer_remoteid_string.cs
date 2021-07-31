using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(9)]
    public class app_indexer_remoteid_string : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("ApplicationIndexerMapping").AlterColumn("RemoteIndexerId").AsString();
        }
    }
}
