using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace EduMindX.SmartBoard.Core;

/// <summary>
/// Web Uygulama Güvenlik Duvarlarının (WAF) veya ters vekillerin (Reverse Proxy)
/// API isteklerini bot sanıp engellemesini önlemek için standart HTTP başlıkları ekleyen handler.
/// </summary>
public sealed class WafSafeHeadersHandler : DelegatingHandler
{
	public WafSafeHeadersHandler(HttpMessageHandler inner)
		: base(inner)
	{
	}

	protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		Apply(request);
		return base.SendAsync(request, cancellationToken);
	}

	/// <summary>
	/// İsteğe standart Accept ve Content-Type başlıklarını uygular.
	/// </summary>
	public static void Apply(HttpRequestMessage request)
	{
		if (request.Headers.Accept.Count == 0)
		{
			request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
		}

		if (request.Content != null && request.Content.Headers.ContentType == null)
		{
			request.Content.Headers.ContentType = new MediaTypeHeaderValue("text/plain")
			{
				CharSet = "utf-8"
			};
		}
	}
}
