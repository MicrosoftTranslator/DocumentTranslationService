using Azure.Core;
using Azure.Core.Pipeline;

namespace DocumentTranslationService
{
    internal class FlightPolicy : HttpPipelineSynchronousPolicy
    {
        private readonly string flightString;
        /// <summary>
        /// Sets the string to be used in a flight
        /// </summary>
        /// <param name="flightString">the string to be used in a flight</param>
        public FlightPolicy(string flightString)
        {
            this.flightString = flightString;
        }

        public override void OnSendingRequest(HttpMessage message)
            {
                if (message.Request.Method.Method == "POST")
                {
                    message.Request.Uri.AppendQuery("flight", flightString);
                }
            }
    }
}
