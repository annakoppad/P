// P semantics test: one machine, "send" to itself in exit function
// This test checks that "unhandled event" exception is not raised by Zing 
// for "halt" event
// "halt" is sent upon executing goto; however, 
// "halt" is not handled, since state Real1_Init is removed once goto is executed
// Compare this test to SendInExitUnhandledEvent.p

event E1 assert 1;

main machine Real1 {
    var test: bool;  //init with "false"
    start state Real1_Init {
        entry { 
			send this, E1;
        }
		on E1 goto Real1_S1;
		exit {  send this, halt; }
	}
	state Real1_S1 {
		entry {
			test = true;
		}
    }
}