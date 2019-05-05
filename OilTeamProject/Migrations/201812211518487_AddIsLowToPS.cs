namespace OilTeamProject.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddIsLowToPS : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.ProductStocks", "IsLow", c => c.Boolean(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.ProductStocks", "IsLow");
        }
    }
}
