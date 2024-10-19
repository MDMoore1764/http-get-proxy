#include <stdio.h>
#include <stdlib.h>
#include <fcntl.h>
#include <string.h>
#include <sys/types.h>
#include <sys/socket.h>
#include <netinet/in.h>
#include <arpa/inet.h>
#include <netdb.h>
#include <time.h>
#include <errno.h>
#include <sys/select.h>

#include "utils.h"

extern int sendrequest(int sd);
extern char *readresponse(int sd);
extern void forwardresponse(int sd, char *msg);
extern int startserver();

int main(int argc, char *argv[])
{
	int servsock; /* server socket descriptor */
	int maxfd;	  /* the largest file descriptor number used for select */

	fd_set livesdset, servsdset, currset; /* set of live client sockets, set of live http server sockets, and the combined set */

	/* table to keep client<->server pairs */
	struct pair *table = malloc(sizeof(struct pair));

	char *msg;

	/* check usage */
	if (argc != 1)
	{
		fprintf(stderr, "usage : %s\n", argv[0]);
		exit(1);
	}

	/* get ready to receive requests */
	servsock = startserver();

	printf("Socket: %d", servsock);

	if (servsock == -1)
	{
		printf("Exiting.\n");
		exit(1);
	}

	printf("1");

	table->next = NULL;

	printf("2");

	/* Use FD_ZERO to initialize these sets as empty/cleared sets. */
	FD_ZERO(&livesdset);

	printf("3");
	FD_ZERO(&servsdset);

	printf("4");
	FD_ZERO(&currset);

	printf("5");

	/* FD_SET adds a file descriptor to the curr set. */
	FD_SET(servsock, &currset);

	printf("6");

	/* The maxfd is initialized to the server socket because at present, we haven't seen anything higher than this. So, it's the max. */
	maxfd = servsock;

	printf("Running.");

	while (1)
	{
		int frsock;

		currset = livesdset;
		for (int i = 0; i <= maxfd; i++)
		{
			/* FD_ISSET checks to see if a given fd is part of the set. Test every value i between 0 and macfd to see if the descriptor is in the servsdset, and if so, add it to he currset. */
			if (FD_ISSET(i, &servsdset) || FD_ISSET(i, &livesdset))
			{
				printf("assigning port to current set: %i", i);
				FD_SET(i, &currset);
			}
		}

		if (select(maxfd + 1, &currset, NULL, NULL, NULL) < 0)
		{
			fprintf(stderr, "Can't select.\n");
			continue;
		}

		/* Iterate over file descriptors starting at 0, up to and including maxfd. */
		for (frsock = 0; frsock <= maxfd; frsock++)
		{
			if (frsock == servsock)
			{
				continue;
			}

			/* Using FD_ISSET to check if frsock is in the live clients list. */
			if (FD_ISSET(frsock, &livesdset))
			{
				printf("Sock is in live client set!");
				/* forward the request */
				int newsd = sendrequest(frsock);
				if (!newsd)
				{
					printf("admin: disconnect from client\n");

					/* Remove from the livesdset, since it's certainly a part of this set. */
					FD_CLR(frsock, &livesdset);

					/* Remove from teh combined set. */
					FD_CLR(frsock, &currset);
				}
				else
				{
					insertpair(table, newsd, frsock);

					/* sendrequest returns the server connection descriptor. I add this to the servsdset. */
					FD_SET(newsd, &servsdset);

					/* If this new sd is > maxfd, our max server descriptor, then this is our new max.*/
					if (newsd > maxfd)
					{
						maxfd = newsd;
					}
				}
			}

			/* Use FD_ISSET to check if the frsock is in the server set.*/
			if (FD_ISSET(frsock, &servsdset))
			{
				printf("Sock is in server set!");
				char *msg;
				struct pair *entry = NULL;
				struct pair *delentry;
				msg = readresponse(frsock);
				if (!msg)
				{
					fprintf(stderr, "error: server died\n");
					exit(1);
				}

				/* forward response to client */
				entry = searchpair(table, frsock);
				if (!entry)
				{
					fprintf(stderr, "error: could not find matching clent sd\n");
					exit(1);
				}

				forwardresponse(entry->clientsd, msg);
				delentry = deletepair(table, entry->serversd);

				/* clear the client sockets used for the connection entry using FD_CLR (removes descriptor from a set). */
				FD_CLR(entry->clientsd, &livesdset);
				FD_CLR(entry->clientsd, &currset);

				/* Clear the server sockets used the connection using FD_CLR. */
				FD_CLR(entry->serversd, &servsdset);
				FD_CLR(entry->serversd, &currset);
			}
		}

		/* input from new client*/
		if (FD_ISSET(servsock, &currset))
		{
			printf("New client detected!");
			struct sockaddr_in caddr;
			socklen_t clen = sizeof(caddr);
			int csd = accept(servsock, (struct sockaddr *)&caddr, &clen);

			if (csd != -1)
			{
				/* Since this is input from a new client, add it to the client list.*/
				FD_SET(csd, &livesdset);

				/* If the new descriptor is higher than maxfd, then set maxfd equal to this descriptor. */
				if (csd > maxfd)
				{
					maxfd = csd;
				}
			}
			else
			{
				perror("accept");
				exit(0);
			}
		}
	}
}
