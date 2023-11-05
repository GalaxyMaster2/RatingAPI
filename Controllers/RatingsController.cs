using Microsoft.AspNetCore.Mvc;

namespace RatingAPI.Controllers
{
    public class RatingsController : Controller
    {
        private readonly ILogger<RatingsController> _logger;

        public RatingsController(ILogger<RatingsController> logger)
        {
            _logger = logger;
        }

        [HttpGet("~/ppai/{hash}/{diff}/{mode}")]
        public ActionResult<double> Get(string hash, int diff, string mode)
        {
            return new InferPublish().GetBlRatings(hash, mode, diff, 1);
        }
    }
}