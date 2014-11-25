// Liveness test: "check passed", however this is a false pass:
// WatchDog monitor is never instantiated, hence, Zing ignores 
// all invocations of it
// TODO: need to issue a warning (or error)
// Compare this test to Liveness_1.p

event UserEvent;
event Done;
event Waiting;
event Computing;

main machine EventHandler
{
       start state WaitForUser
       {
            entry { 
				//new WatchDog();
				monitor WatchDog, Waiting;
				send this, UserEvent;
				}
            on UserEvent goto HandleEvent;
       }
  
       state HandleEvent
       {
            entry { 
				monitor WatchDog, Computing;
				send this, Done;
				}			
            on Done goto WaitForUser;
       }
}

monitor WatchDog
{
      start cold state CanGetUserInput
      {
             on Waiting goto CanGetUserInput;
             on Computing goto CannotGetUserInput;
      } 
	  hot state CannotGetUserInput
     {
             on Waiting goto CanGetUserInput;
             on Computing goto CannotGetUserInput;
     }
}
