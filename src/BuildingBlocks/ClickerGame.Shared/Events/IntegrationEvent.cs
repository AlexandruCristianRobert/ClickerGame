using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClickerGame.Shared.Events
{
    public abstract class Integrationevent
    {
        public Integrationevent()
        {
            Id = Guid.NewGuid();
            CreationDate = DateTime.UtcNow;
        }

        public Guid Id { get; private set; }
        public DateTime CreationDate { get; private set; }

    }
}
