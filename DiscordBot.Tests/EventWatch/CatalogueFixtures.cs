namespace DiscordBot.Tests.EventWatch;

internal static class CatalogueFixtures
{
    public static string Empty() =>
        """{"status":"success","totalPages":1,"total":0,"pageSize":50,"page":1,"items":[]}""";

    public static string GenericPackage() =>
        """{"status":"success","totalPages":1,"total":2,"pageSize":50,"page":1,"items":[{"id":"CDYQ7TOEZE","seriesId":"KWCQ65LFBX","status":"ON_SALE","name":"UEFA Conference League - Biļete uz trīs mājas spēlēm","sluggedName":"uefa-conference-league-bilete-uz-tris-majas-spelem","venue":{"name":"Skonto stadions","country":"LV","city":"Rīga"}},{"id":"GU2ZXNPG4W","seriesId":"D7MJ6X3RYG","status":"ON_SALE","name":"Sezonas biļete visām FC Riga Māja Spēlēm","sluggedName":"sezonas-bilete-visam-fc-riga-maja-spelem","venue":{"name":"Skonto stadions","country":"LV","city":"Rīga"}}]}""";

    public static string WithKairat() =>
        """{"status":"success","totalPages":1,"total":3,"pageSize":50,"page":1,"items":[{"id":"CDYQ7TOEZE","seriesId":"KWCQ65LFBX","status":"ON_SALE","name":"UEFA Conference League - Biļete uz trīs mājas spēlēm","sluggedName":"uefa-conference-league-bilete-uz-tris-majas-spelem","venue":{"name":"Skonto stadions","country":"LV","city":"Rīga"}},{"id":"GU2ZXNPG4W","seriesId":"D7MJ6X3RYG","status":"ON_SALE","name":"Sezonas biļete visām FC Riga Māja Spēlēm","sluggedName":"sezonas-bilete-visam-fc-riga-maja-spelem","venue":{"name":"Skonto stadions","country":"LV","city":"Rīga"}},{"id":"KAIRAT01","status":"ON_SALE","name":"Riga FC vs Kairat Almaty","sluggedName":"riga-fc-vs-kairat-almaty","venue":{"name":"Skonto stadions","country":"LV","city":"Rīga"}}]}""";

    public static string WithAtalanta() =>
        """{"status":"success","totalPages":1,"total":3,"pageSize":50,"page":1,"items":[{"id":"CDYQ7TOEZE","seriesId":"KWCQ65LFBX","status":"ON_SALE","name":"UEFA Conference League - Biļete uz trīs mājas spēlēm","sluggedName":"uefa-conference-league-bilete-uz-tris-majas-spelem","venue":{"name":"Skonto stadions","country":"LV","city":"Rīga"}},{"id":"GU2ZXNPG4W","seriesId":"D7MJ6X3RYG","status":"ON_SALE","name":"Sezonas biļete visām FC Riga Māja Spēlēm","sluggedName":"sezonas-bilete-visam-fc-riga-maja-spelem","venue":{"name":"Skonto stadions","country":"LV","city":"Rīga"}},{"id":"ATALANTA01","status":"ON_SALE","name":"Riga FC vs Atalanta","sluggedName":"riga-fc-vs-atalanta","venue":{"name":"Skonto stadions","country":"LV","city":"Rīga"}}]}""";
}
